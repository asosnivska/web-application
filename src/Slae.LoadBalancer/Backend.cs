namespace Slae.LoadBalancer;

public class Backend
{
    private volatile bool _isHealthy;
    private int _activeRequests;
    private long _totalRequests;

    public Backend(Uri url)
    {
        Url = url;
    }

    public Uri Url { get; }

    public bool IsHealthy
    {
        get => _isHealthy;
        set => _isHealthy = value;
    }

    public int ActiveRequests => Volatile.Read(ref _activeRequests);

    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    public void RequestStarted()
    {
        Interlocked.Increment(ref _activeRequests);
        Interlocked.Increment(ref _totalRequests);
    }

    public void RequestFinished()
    {
        Interlocked.Decrement(ref _activeRequests);
    }
}