using Slae.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Domain.Entities
{
    public class SlaeTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public int Dimension { get; set; }
        public string ParamsJson { get; set; } = null!;

        public SlaeTaskStatus Status { get; set; } = SlaeTaskStatus.Pending;
        public int Progress { get; set; }
        public bool IsCanceled { get; set; }

        public string? ServerId { get; set; }
        public DateTime? WorkerLastReportedAt { get; set; }
        public int Attempts { get; set; }

        public string? ResultJson { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public uint RowVersion { get; set; }
    }
}
