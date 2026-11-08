using Microsoft.Extensions.Options;

namespace Slae.LoadBalancer;

public class HealthCheckService : BackgroundService
{
    private readonly BackendPool _pool;
    private readonly HttpClient _client;
    private readonly LoadBalancerOptions _options;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        BackendPool pool, HttpClient client, IOptions<LoadBalancerOptions> options, ILogger<HealthCheckService> logger)
    {
        _pool = pool;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HealthCheckSeconds));

        try
        {
            do
            {
                await Task.WhenAll(_pool.Backends.Select(b => CheckAsync(b, stoppingToken)));
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CheckAsync(Backend backend, CancellationToken stoppingToken)
    {
        bool healthy;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.HealthTimeoutSeconds));

            using var response = await _client.GetAsync(new Uri(backend.Url, "/health"), cts.Token);
            healthy = response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            stoppingToken.ThrowIfCancellationRequested();
            healthy = false;
        }

        if (healthy != backend.IsHealthy)
        {
            backend.IsHealthy = healthy;
            _logger.Log(healthy ? LogLevel.Information : LogLevel.Warning, "Backend {Url} is now {State}", backend.Url, healthy ? "HEALTHY" : "DOWN");
        }
    }
}