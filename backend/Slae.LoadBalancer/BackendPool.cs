using Microsoft.Extensions.Options;

namespace Slae.LoadBalancer;

public class BackendPool
{
    private readonly Backend[] _backends;
    private int _counter = -1;

    public BackendPool(IOptions<LoadBalancerOptions> options)
    {
        var urls = options.Value.Backends;

        if (urls.Count == 0)
        {
            throw new InvalidOperationException("LoadBalancer:Backends is empty. Add at least one server address.");
        }

        _backends = urls.Select(u => new Backend(new Uri(u))).ToArray();
    }

    public IReadOnlyList<Backend> Backends => _backends;

    public bool TryPick(ISet<Backend> alreadyTried, out Backend backend)
    {
        uint start = (uint)Interlocked.Increment(ref _counter);

        for (uint i = 0; i < _backends.Length; i++)
        {
            var candidate = _backends[(start + i) % (uint)_backends.Length];

            if (candidate.IsHealthy && !alreadyTried.Contains(candidate))
            {
                backend = candidate;
                return true;
            }
        }

        backend = null!;
        return false;
    }
}