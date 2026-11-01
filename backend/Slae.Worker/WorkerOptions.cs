namespace Slae.Worker;

public class WorkerOptions
{
    public const string SectionName = "Worker";

    public int MaxThreads { get; set; }

    public int PollIntervalMs { get; set; } = 1000;

    public int HeartbeatSeconds { get; set; } = 5;

    public int StaleAfterSeconds { get; set; } = 30;

    public int RecoverEverySeconds { get; set; } = 15;

    public int MaxAttempts { get; set; } = 3;
}