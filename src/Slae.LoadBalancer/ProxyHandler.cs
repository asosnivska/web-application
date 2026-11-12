using Microsoft.Extensions.Options;

namespace Slae.LoadBalancer;

public class ProxyHandler
{
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
        "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Host"
    };

    private readonly BackendPool _pool;
    private readonly HttpClient _client;
    private readonly LoadBalancerOptions _options;
    private readonly ILogger<ProxyHandler> _logger;

    public ProxyHandler(
        BackendPool pool, HttpClient client, IOptions<LoadBalancerOptions> options, ILogger<ProxyHandler> logger)
    {
        _pool = pool;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        byte[] body = await ReadBodyAsync(context.Request); 

        var tried = new HashSet<Backend>();

        while (_pool.TryPick(tried, out var backend))
        {
            tried.Add(backend);
            backend.RequestStarted();

            try
            {
                using var request = BuildRequest(context, backend, body);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                await CopyResponseAsync(context, response, cts.Token);

                _logger.LogInformation("{Method} {Path} -> {Backend} {Status}",
                    context.Request.Method, context.Request.Path, backend.Url, (int)response.StatusCode);
                return;
            }
            catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConnectionError)
            {
                backend.IsHealthy = false;
                _logger.LogWarning("Backend {Url} refused connection, trying another one", backend.Url);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Backend {Url} failed", backend.Url);
                await WriteErrorAsync(context, StatusCodes.Status502BadGateway, "Backend returned an invalid response.");
                return;
            }
            catch (OperationCanceledException)
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogError("Backend {Url} timed out", backend.Url);
                await WriteErrorAsync(context, StatusCodes.Status504GatewayTimeout, "Backend did not respond in time.");
                return;
            }
            finally
            {
                backend.RequestFinished();
            }
        }

        await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "No healthy backend available.");
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static HttpRequestMessage BuildRequest(HttpContext context, Backend backend, byte[] body)
    {
        var source = context.Request;
        var target = new Uri(backend.Url, $"{source.Path}{source.QueryString}");
        var request = new HttpRequestMessage(new HttpMethod(source.Method), target);

        if (body.Length > 0)
        {
            request.Content = new ByteArrayContent(body);
        }

        foreach (var header in source.Headers)
        {
            if (HopByHop.Contains(header.Key) || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
            }
        }

        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string forwardedFor = source.Headers.TryGetValue("X-Forwarded-For", out var previous)
            ? $"{previous}, {clientIp}"
            : clientIp;

        request.Headers.Remove("X-Forwarded-For");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", source.Scheme);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", source.Host.ToString());

        return request;
    }

    private static async Task CopyResponseAsync(HttpContext context, HttpResponseMessage response, CancellationToken ct)
    {
        context.Response.StatusCode = (int)response.StatusCode;

        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (!HopByHop.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        await response.Content.CopyToAsync(context.Response.Body, ct);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}