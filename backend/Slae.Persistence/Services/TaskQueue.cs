using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Slae.Application.Interfaces;
using Slae.Domain.Enums;

namespace Slae.Persistence.Services
{
    public class TaskQueue : ITaskQueue
    {
        private readonly NpgsqlDataSource _ds;

        public TaskQueue(NpgsqlDataSource ds) => _ds = ds;

        private static readonly int Pending = (int)SlaeTaskStatus.Pending;
        private static readonly int InProgress = (int)SlaeTaskStatus.InProgress;
        private static readonly int Completed = (int)SlaeTaskStatus.Completed;
        private static readonly int Canceled = (int)SlaeTaskStatus.Canceled;
        private static readonly int Failed = (int)SlaeTaskStatus.Failed;

        public async Task<ClaimedTask?> ClaimNextAsync(string serverId, CancellationToken ct)
        {
            string sql = $$"""
            UPDATE "SlaeTasks"
            SET "Status" = {{InProgress}},
                "ServerId" = @server,
                "WorkerLastReportedAt" = now(),
                "Attempts" = "Attempts" + 1
            WHERE "Id" = (
                SELECT "Id" FROM "SlaeTasks"
                WHERE "Status" = {{Pending}} AND NOT "IsCanceled"
                ORDER BY "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING "Id", "UserId", "Dimension", "ParamsJson", "Attempts";
            """;

            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("server", serverId);
            await using var r = await cmd.ExecuteReaderAsync(ct);

            if (!await r.ReadAsync(ct))
            {
                return null;
            }

            return new ClaimedTask(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetInt32(4));
        }

        public async Task<ProgressResult> ReportProgressAsync(Guid id, string serverId, int progress, CancellationToken ct)
        {
            string sql = $$"""
            UPDATE "SlaeTasks"
            SET "Progress" = @p, "WorkerLastReportedAt" = now()
            WHERE "Id" = @id AND "ServerId" = @server AND "Status" = {{InProgress}}
            RETURNING "IsCanceled";
            """;
            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("p", Math.Clamp(progress, 0, 100));
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("server", serverId);

            object? res = await cmd.ExecuteScalarAsync(ct);

            if (res is null) 
            {
                return ProgressResult.LostOwnership;
            }
            return (bool)res ? ProgressResult.CancelRequested : ProgressResult.Ok;
        }

        public async Task<bool> CompleteAsync(Guid id, string serverId, string resultJson, CancellationToken ct)
        {
            string sql = $$"""
            UPDATE "SlaeTasks"
            SET "Status" = {{Completed}}, "Progress" = 100, "ResultJson" = @result,
                "CompletedAt" = now(), "WorkerLastReportedAt" = now()
            WHERE "Id" = @id AND "ServerId" = @server AND "Status" = {{InProgress}};
            """;

            await using var cmd = _ds.CreateCommand(sql);

            cmd.Parameters.Add(new NpgsqlParameter("result", NpgsqlDbType.Jsonb) { Value = resultJson });
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("server", serverId);
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }

        public async Task<bool> FailAsync(Guid id, string serverId, string error, CancellationToken ct)
        {
            string sql = $$"""
            UPDATE "SlaeTasks"
            SET "Status" = {{Failed}}, "ErrorMessage" = @err, "CompletedAt" = now()
            WHERE "Id" = @id AND "ServerId" = @server AND "Status" = {{InProgress}};
            """;

            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("err", error.Length > 2000 ? error[..2000] : error);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("server", serverId);
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }

        public async Task<bool> MarkCanceledAsync(Guid id, string serverId, CancellationToken ct)
        {
            string sql = $$"""
            UPDATE "SlaeTasks"
            SET "Status" = {{Canceled}}, "CompletedAt" = now()
            WHERE "Id" = @id AND "ServerId" = @server AND "Status" = {{InProgress}};
            """;

            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("server", serverId);

            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }

        public async Task<bool> ReleaseAsync(Guid id, string serverId, CancellationToken ct)
        {
            string sql = $$"""
            UPDATE "SlaeTasks"
            SET "Status" = {{Pending}}, "ServerId" = NULL, "Progress" = 0,
                "Attempts" = GREATEST("Attempts" - 1, 0)
            WHERE "Id" = @id AND "ServerId" = @server AND "Status" = {{InProgress}};
            """;

            await using var cmd = _ds.CreateCommand(sql);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("server", serverId);

            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }

        public async Task<int> RecoverStaleAsync(TimeSpan staleAfter, int maxAttempts, CancellationToken ct)
        {
            string canceledSql = $$"""
            UPDATE "SlaeTasks" SET "Status" = {{Canceled}}, "CompletedAt" = now()
            WHERE "Status" = {{InProgress}} AND "WorkerLastReportedAt" < now() - @stale AND "IsCanceled";
            """;
            string failedSql = $$"""
            UPDATE "SlaeTasks"
            SET "Status" = {{Failed}}, "CompletedAt" = now(),
                "ErrorMessage" = 'Worker stopped without a result, no attempts left.'
            WHERE "Status" = {{InProgress}} AND "WorkerLastReportedAt" < now() - @stale AND "Attempts" >= @max;
            """;
            string requeueSql = $$"""
            UPDATE "SlaeTasks" SET "Status" = {{Pending}}, "ServerId" = NULL, "Progress" = 0
            WHERE "Status" = {{InProgress}} AND "WorkerLastReportedAt" < now() - @stale;
            """;

            await using var conn = await _ds.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            int total = 0;
            foreach (var sql in new[] { canceledSql, failedSql, requeueSql })
            {
                await using var cmd = new NpgsqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("stale", staleAfter);
                
                if (sql == failedSql) 
                {
                    cmd.Parameters.AddWithValue("max", maxAttempts);
                }
                total += await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);

            return total;
        }
    }

}
