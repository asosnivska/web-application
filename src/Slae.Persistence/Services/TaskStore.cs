using Microsoft.EntityFrameworkCore;
using Slae.Application.Interfaces;
using Slae.Application.Models;
using Slae.Domain.Entities;
using Slae.Domain.Enums;
using System.Text.Json;

namespace Slae.Persistence.Services
{
    public class TaskStore : ITaskStore
    {
        private readonly SlaeDbContext _db;
        private const int WorkerCount = 2;


        public TaskStore(SlaeDbContext db)
        {
            _db = db;
        }

        public async Task<CreateTaskResult> CreateAsync(Guid userId, JacobiParams p, CancellationToken ct)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({userId.ToString()}))", ct);

            int? userLimit = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => (int?)u.MaxConcurrentTasks)
                .FirstOrDefaultAsync(ct);

            if (userLimit is null)
            {
                return new CreateTaskResult(CreateStatus.UserNotFound, null, 0, 0);
            }

            int active = await _db.Tasks.CountAsync(t => t.UserId == userId
                && (t.Status == SlaeTaskStatus.Pending || t.Status == SlaeTaskStatus.InProgress), ct);

            if (active >= userLimit.Value)
            {
                return new CreateTaskResult(CreateStatus.LimitReached, null, active, userLimit.Value);
            }

            var task = new SlaeTask
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Dimension = p.Dimension,
                ParamsJson = JsonSerializer.Serialize(p),
                Status = SlaeTaskStatus.Pending,
                Progress = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new CreateTaskResult(CreateStatus.Created, task.Id, active + 1, userLimit.Value);
        }

        private async Task<(int? QueuePosition, int? EstimatedWaitSeconds)> GetQueueInfoAsync(SlaeTaskStatus status, DateTime createdAt, CancellationToken ct)
        {
            if (status != SlaeTaskStatus.Pending)
            {
                return (null, null);
            }

            int ahead = await _db.Tasks.CountAsync(t =>
                t.Status == SlaeTaskStatus.Pending && !t.IsCanceled && t.CreatedAt < createdAt, ct);

            int runningCount = await _db.Tasks.CountAsync(t =>
                t.Status == SlaeTaskStatus.InProgress, ct);

            if (ahead + runningCount < WorkerCount)
            {
                return (ahead + 1, 0);
            }

            var recent = await _db.Tasks.AsNoTracking()
                .Where(t => t.Status == SlaeTaskStatus.Completed && t.StartedAt != null && t.CompletedAt != null)
                .OrderByDescending(t => t.CompletedAt)
                .Take(20)
                .Select(t => new { t.StartedAt, t.CompletedAt })
                .ToListAsync(ct);

            double avgSeconds = recent.Count > 0
                ? recent.Average(x => (x.CompletedAt!.Value - x.StartedAt!.Value).TotalSeconds)
                : 30;

            double totalWorkSeconds = (ahead + runningCount) * avgSeconds;
            int estimatedWaitSeconds = (int)Math.Ceiling(totalWorkSeconds / WorkerCount);

            return (ahead + 1, estimatedWaitSeconds);
        }

        public async Task<TaskDetailsDto?> GetAsync(Guid userId, Guid taskId, CancellationToken ct)
        {
            var t = await _db.Tasks.AsNoTracking()
                .Where(x => x.Id == taskId && x.UserId == userId)
                .Select(x => new TaskDetailsDto(x.Id, x.Status, x.Progress, x.Dimension,
                    x.CreatedAt, x.CompletedAt, x.ResultJson, x.ErrorMessage, null, null))
                .FirstOrDefaultAsync(ct);

            if (t is null)
            {
                return null;
            }

            var (queuePosition, estimatedWaitSeconds) = await GetQueueInfoAsync(t.Status, t.CreatedAt, ct);
            return t with { QueuePosition = queuePosition, EstimatedWaitSeconds = estimatedWaitSeconds };
        }

        public async Task<List<TaskSummaryDto>> ListAsync(Guid userId, int skip, int take, CancellationToken ct)
        {
            var items = await _db.Tasks.AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 100))
                .Select(t => new TaskSummaryDto(t.Id, t.Status, t.Progress, t.Dimension,
                    t.CreatedAt, t.CompletedAt, null, null))
                .ToListAsync(ct);

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Status == SlaeTaskStatus.Pending)
                {
                    var (queuePosition, estimatedWaitSeconds) = await GetQueueInfoAsync(items[i].Status, items[i].CreatedAt, ct);
                    items[i] = items[i] with { QueuePosition = queuePosition, EstimatedWaitSeconds = estimatedWaitSeconds };
                }
            }

            return items;
        }

        public async Task<CancelResult> CancelAsync(Guid userId, Guid taskId, CancellationToken ct)
        {
            DateTime? now = DateTime.UtcNow;

            int changed = await _db.Tasks
                .Where(t => t.Id == taskId && t.UserId == userId && t.Status == SlaeTaskStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, SlaeTaskStatus.Canceled)
                    .SetProperty(t => t.IsCanceled, true)
                    .SetProperty(t => t.CompletedAt, now), ct);

            if (changed == 1)
            {
                return CancelResult.CanceledImmediately;
            }

            changed = await _db.Tasks
                .Where(t => t.Id == taskId && t.UserId == userId && t.Status == SlaeTaskStatus.InProgress)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsCanceled, true), ct);

            if (changed == 1)
            {
                return CancelResult.CancelRequested;
            }

            bool exists = await _db.Tasks.AnyAsync(t => t.Id == taskId && t.UserId == userId, ct);
            return exists ? CancelResult.AlreadyFinished : CancelResult.NotFound;
        }
    }
}