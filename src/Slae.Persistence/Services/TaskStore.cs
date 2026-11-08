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

        public Task<TaskDetailsDto?> GetAsync(Guid userId, Guid taskId, CancellationToken ct) =>
            _db.Tasks.AsNoTracking()
                .Where(t => t.Id == taskId && t.UserId == userId)
                .Select(t => new TaskDetailsDto(t.Id, t.Status, t.Progress, t.Dimension,
                    t.CreatedAt, t.CompletedAt, t.ResultJson, t.ErrorMessage))
                .FirstOrDefaultAsync(ct);

        public Task<List<TaskSummaryDto>> ListAsync(Guid userId, int skip, int take, CancellationToken ct) =>
            _db.Tasks.AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 100))
                .Select(t => new TaskSummaryDto(t.Id, t.Status, t.Progress, t.Dimension, t.CreatedAt, t.CompletedAt))
                .ToListAsync(ct);

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
