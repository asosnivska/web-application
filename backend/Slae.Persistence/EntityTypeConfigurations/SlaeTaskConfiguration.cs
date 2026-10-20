using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Slae.Domain.Entities;
using Slae.Domain.Enums;

namespace Slae.Persistence.EntityTypeConfigurations
{
    internal class SlaeTaskConfiguration: IEntityTypeConfiguration<SlaeTask>
    {

        private static readonly int Pending = (int)SlaeTaskStatus.Pending;
        private static readonly int InProgress = (int)SlaeTaskStatus.InProgress;
        private static readonly int Completed = (int)SlaeTaskStatus.Completed;
        private static readonly int Canceled = (int)SlaeTaskStatus.Canceled;
        private static readonly int Failed = (int)SlaeTaskStatus.Failed;

        private const string ColProgress = "\"Progress\"";
        private const string ColDimension = "\"Dimension\"";
        private const string ColAttempts = "\"Attempts\"";
        private const string ColStatus = "\"Status\"";
        private const string ColServerId = "\"ServerId\"";
        private const string ColCompletedAt = "\"CompletedAt\"";
        public void Configure(EntityTypeBuilder<SlaeTask> builder)
        {
            builder.ToTable("SlaeTasks", t =>
            {
                t.HasCheckConstraint("CK_SlaeTasks_Progress_Range", $"{ColProgress} BETWEEN 0 AND 100");

                t.HasCheckConstraint("CK_SlaeTasks_Dimension_Positive", $"{ColDimension} > 0");

                t.HasCheckConstraint("CK_SlaeTasks_Attempts_NonNegative", $"{ColAttempts} >= 0");

                t.HasCheckConstraint("CK_SlaeTasks_Status_Valid",
                    $"{ColStatus} IN ({Pending}, {InProgress}, {Completed}, {Canceled}, {Failed})");

                t.HasCheckConstraint("CK_SlaeTasks_InProgress_HasServer",
                    $"{ColStatus} <> {InProgress} OR {ColServerId} IS NOT NULL");

                t.HasCheckConstraint("CK_SlaeTasks_Finished_HasCompletedAt",
                    $"{ColStatus} NOT IN ({Completed}, {Canceled}, {Failed}) OR {ColCompletedAt} IS NOT NULL");
            });


            builder.HasKey(st => st.Id);

            builder.Property(st => st.Id).ValueGeneratedNever();

            builder.Property(st => st.ParamsJson).HasColumnType("jsonb").IsRequired();

            builder.Property(st => st.ResultJson).HasColumnType("jsonb");

            builder.Property(st => st.Status).HasConversion<int>();

            builder.Property(st => st.ServerId).HasMaxLength(100);
            builder.Property(st => st.ErrorMessage).HasMaxLength(2000);

            builder.Property(st => st.RowVersion).IsRowVersion();

            builder.HasIndex(st => st.CreatedAt)
                .HasFilter($"{ColStatus} = {Pending}")
                .HasDatabaseName("IX_SlaeTasks_Queue");

            builder.HasIndex(st => new { st.UserId, st.Status })
                .HasDatabaseName("IX_SlaeTasks_UserId_Status");

            builder.HasIndex(st => new { st.UserId, st.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_SlaeTasks_UserId_CreatedAt");

            builder.HasIndex(st => st.WorkerLastReportedAt)
                .HasFilter($"{ColStatus} = {InProgress}")
                .HasDatabaseName("IX_SlaeTasks_InProgress_Heartbeat");

            builder.HasOne(st => st.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(st => st.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
