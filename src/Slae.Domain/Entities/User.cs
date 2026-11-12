using System;
using System.Collections.Generic;
using System.Text;

namespace Slae.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int MaxConcurrentTasks { get; set; } = 3;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SlaeTask> Tasks { get; set; } = new List<SlaeTask>();
    }
}
