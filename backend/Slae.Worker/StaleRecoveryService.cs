using Microsoft.Extensions.Options;
using Slae.Application.Interfaces;

namespace Slae.Worker;

public class StaleRecoveryService : BackgroundService
{
    private readonly ITaskQueue _queue;
    private readonly WorkerOptions _options;
    private readonly ILogger<StaleRecoveryService> _logger;

    public StaleRecoveryService(ITaskQueue queue, IOptions<WorkerOptions> options, ILogger<StaleRecoveryService> logger)
    {
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.RecoverEverySeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    int changed = await _queue.RecoverStaleAsync(
                        TimeSpan.FromSeconds(_options.StaleAfterSeconds), _options.MaxAttempts, stoppingToken);

                    if (changed > 0)
                    {
                        _logger.LogWarning("Recovered {Count} stale task(s)", changed);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Stale task recovery failed, will retry");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}