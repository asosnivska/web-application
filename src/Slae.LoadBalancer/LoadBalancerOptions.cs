namespace Slae.LoadBalancer;

public class LoadBalancerOptions
{
    public const string SectionName = "LoadBalancer";

    public List<string> Backends { get; set; } = new();

    public int HealthCheckSeconds { get; set; } = 3;

    public int HealthTimeoutSeconds { get; set; } = 2;

    public int RequestTimeoutSeconds { get; set; } = 30;
}