using System;
using System.Collections.Generic;
using System.Text;
using Slae.Application.Models;
using Slae.Domain.Enums;

namespace Slae.Application.Interfaces
{
    public enum CreateStatus 
    { 
        Created, 
        LimitReached, 
        UserNotFound 
    }

    public record CreateTaskResult(CreateStatus Status, Guid? TaskId, int Active, int Limit);

    public enum CancelResult 
    { 
        NotFound,
        CanceledImmediately,
        CancelRequested,
        AlreadyFinished
    }

    public record TaskSummaryDto(
        Guid Id, SlaeTaskStatus Status, int Progress, int Dimension,
        DateTime CreatedAt, DateTime? CompletedAt);

    public record TaskDetailsDto(
        Guid Id, SlaeTaskStatus Status, int Progress, int Dimension,
        DateTime CreatedAt, DateTime? CompletedAt,
        string? ResultJson, string? ErrorMessage);

    public interface ITaskStore
    {
        Task<CreateTaskResult> CreateAsync(Guid userId, JacobiParams p, CancellationToken ct);
        Task<TaskDetailsDto?> GetAsync(Guid userId, Guid taskId, CancellationToken ct);
        Task<List<TaskSummaryDto>> ListAsync(Guid userId, int skip, int take, CancellationToken ct);
        Task<CancelResult> CancelAsync(Guid userId, Guid taskId, CancellationToken ct);
    }
}
