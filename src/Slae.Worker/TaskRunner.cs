using System.Text.Json;
using Microsoft.Extensions.Options;
using Slae.Application.Interfaces;
using Slae.Application.Models;
using Slae.Application.Services;

namespace Slae.Worker;

public class TaskRunner
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITaskQueue _queue;
    private readonly WorkerOptions _options;
    private readonly string _serverId;
    private readonly ILogger<TaskRunner> _logger;

    public TaskRunner(ITaskQueue queue, IOptions<WorkerOptions> options, WorkerIdentity identity, ILogger<TaskRunner> logger)
    {
        _queue = queue;
        _options = options.Value;
        _serverId = identity.ServerId;
        _logger = logger;
    }

    public async Task RunAsync(ClaimedTask task, CancellationToken stoppingToken)
    {
        JacobiParams? p;
        try
        {
            p = JsonSerializer.Deserialize<JacobiParams>(task.ParamsJson);
        }
        catch (JsonException ex)
        {
            await _queue.FailAsync(task.Id, _serverId, "Invalid task parameters: " + ex.Message, CancellationToken.None);
            return;
        }

        if (p is null)
        {
            await _queue.FailAsync(task.Id, _serverId, "Task parameters are empty.", CancellationToken.None);
            return;
        }

        List<string> errors = TaskParamsValidator.Validate(p);
        if (errors.Count > 0)
        {
            await _queue.FailAsync(task.Id, _serverId, string.Join(" ", errors), CancellationToken.None);
            return;
        }

        var state = new RunState();
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        runCts.CancelAfter(TimeSpan.FromSeconds(TaskLimits.MaxRunSeconds));

        using var heartbeatCts = new CancellationTokenSource();
        Task heartbeat = HeartbeatAsync(task, state, runCts, heartbeatCts.Token);

        try
        {
            var (a, b) = SlaeGenerator.Build(p);

            JacobiResult result = await Task.Run(() => JacobiSolver.Solve(
                a, b, p.Dimension, p.Epsilon, p.MaxIterations, GetThreads(), state, runCts.Token));

            await StopHeartbeatAsync(heartbeatCts, heartbeat);

            string json = JsonSerializer.Serialize(result, ResultJsonOptions);
            bool saved = await _queue.CompleteAsync(task.Id, _serverId, json, CancellationToken.None);

            if (saved)
            {
                _logger.LogInformation("Task {TaskId} completed in {Seconds:F2} s, {Iterations} iterations, solved: {Solved}",
                    task.Id, result.ElapsedSeconds, result.Iterations, result.IsSolved);
            }
            else
            {
                _logger.LogWarning("Task {TaskId} finished, but the result was not saved (task was taken from this worker)", task.Id);
            }
        }
        catch (OperationCanceledException)
        {
            await StopHeartbeatAsync(heartbeatCts, heartbeat);
            await HandleCancellationAsync(task, state, stoppingToken);
        }
        catch (Exception ex)
        {
            await StopHeartbeatAsync(heartbeatCts, heartbeat);
            _logger.LogError(ex, "Task {TaskId} failed", task.Id);
            await _queue.FailAsync(task.Id, _serverId, ex.Message, CancellationToken.None);
        }
    }

    private async Task HandleCancellationAsync(ClaimedTask task, RunState state, CancellationToken stoppingToken)
    {
        if (state.LostOwnership)
        {
            _logger.LogWarning("Task {TaskId} was taken from this worker, result is dropped", task.Id);
            return;
        }

        if (state.UserCanceled)
        {
            await _queue.MarkCanceledAsync(task.Id, _serverId, CancellationToken.None);
            _logger.LogInformation("Task {TaskId} canceled by user", task.Id);
            return;
        }

        if (stoppingToken.IsCancellationRequested)
        {
            await _queue.ReleaseAsync(task.Id, _serverId, CancellationToken.None);
            _logger.LogInformation("Task {TaskId} returned to the queue because the worker is stopping", task.Id);
            return;
        }

        await _queue.FailAsync(task.Id, _serverId,
            $"Time limit exceeded: task ran longer than {TaskLimits.MaxRunSeconds} seconds.", CancellationToken.None);
        _logger.LogWarning("Task {TaskId} stopped: time limit exceeded", task.Id);
    }

    private async Task HeartbeatAsync(ClaimedTask task, RunState state, CancellationTokenSource runCts, CancellationToken heartbeatToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(heartbeatToken))
            {
                try
                {
                    ProgressResult r = await _queue.ReportProgressAsync(task.Id, _serverId, state.Progress, heartbeatToken);

                    if (r == ProgressResult.CancelRequested)
                    {
                        state.UserCanceled = true;
                        runCts.Cancel();
                    }
                    else if (r == ProgressResult.LostOwnership)
                    {
                        state.LostOwnership = true;
                        runCts.Cancel();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Heartbeat for task {TaskId} failed, will retry", task.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task StopHeartbeatAsync(CancellationTokenSource heartbeatCts, Task heartbeat)
    {
        await heartbeatCts.CancelAsync();
        await heartbeat;
    }

    private int GetThreads() => _options.MaxThreads > 0 ? _options.MaxThreads : Environment.ProcessorCount;

    private sealed class RunState : IProgress<int>
    {
        private int _progress;

        public int Progress => Volatile.Read(ref _progress);
        public volatile bool UserCanceled;
        public volatile bool LostOwnership;

        public void Report(int value) => Volatile.Write(ref _progress, value);
    }
}