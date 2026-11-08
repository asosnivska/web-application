using Slae.LoadBalancer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LoadBalancerOptions>(builder.Configuration.GetSection(LoadBalancerOptions.SectionName));
builder.Services.AddSingleton<BackendPool>();
builder.Services.AddSingleton<ProxyHandler>();
builder.Services.AddHostedService<HealthCheckService>();

builder.Services.AddSingleton(new HttpClient(new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
    AutomaticDecompression = System.Net.DecompressionMethods.None,
    ConnectTimeout = TimeSpan.FromSeconds(2)
})
{
    Timeout = Timeout.InfiniteTimeSpan
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/api/{**rest}", (HttpContext context, ProxyHandler proxy) => proxy.HandleAsync(context));
app.Map("/health", (HttpContext context, ProxyHandler proxy) => proxy.HandleAsync(context));

app.MapGet("/lb/status", (BackendPool pool) => pool.Backends.Select(b => new
{
    url = b.Url.ToString(),
    healthy = b.IsHealthy,
    activeRequests = b.ActiveRequests,
    totalRequests = b.TotalRequests
}));

app.Run();
