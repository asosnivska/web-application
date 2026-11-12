using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Application.Interfaces
{
    public record ClaimedTask(Guid Id, Guid UserId, int Dimension, string ParamsJson, int Attempts);

    public enum ProgressResult 
    { 
        Ok, 
        CancelRequested, 
        LostOwnership 
    }

    public interface ITaskQueue
    {
        Task<ClaimedTask?> ClaimNextAsync(string serverId, CancellationToken ct);
        Task<ProgressResult> ReportProgressAsync(Guid id, string serverId, int progress, CancellationToken ct);

        Task<bool> CompleteAsync(Guid id, string serverId, string resultJson, CancellationToken ct);
        Task<bool> FailAsync(Guid id, string serverId, string error, CancellationToken ct);
        Task<bool> MarkCanceledAsync(Guid id, string serverId, CancellationToken ct);

        Task<bool> ReleaseAsync(Guid id, string serverId, CancellationToken ct);

        Task<int> RecoverStaleAsync(TimeSpan staleAfter, int maxAttempts, CancellationToken ct);
    }
}
