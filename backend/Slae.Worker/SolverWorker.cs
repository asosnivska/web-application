using Microsoft.Extensions.Options;
using Slae.Application.Interfaces;

namespace Slae.Worker;

public class SolverWorker : BackgroundService
{
    private readonly ITaskQueue _queue;
    private readonly TaskRunner _runner;
    private readonly WorkerOptions _options;
    private readonly string _serverId;
    private readonly ILogger<SolverWorker> _logger;

    public SolverWorker(ITaskQueue queue, TaskRunner runner, IOptions<WorkerOptions> options,
        WorkerIdentity identity, ILogger<SolverWorker> logger)
    {
        _queue = queue;
        _runner = runner;
        _options = options.Value;
        _serverId = identity.ServerId;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {ServerId} started", _serverId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ClaimedTask? task = await _queue.ClaimNextAsync(_serverId, stoppingToken);

                if (task is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMs), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Task {TaskId} claimed (dimension {Dimension}, attempt {Attempt})",
                    task.Id, task.Dimension, task.Attempts);

                await _runner.RunAsync(task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error, pausing for 5 seconds");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Worker {ServerId} stopped", _serverId);
    }
}