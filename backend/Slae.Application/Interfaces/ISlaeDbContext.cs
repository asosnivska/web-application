using Slae.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Slae.Application.Interfaces
{
    public interface ISlaeDbContext
    {
        DbSet<SlaeTask> Tasks { get; set; }

        DbSet<User> Users { get; set; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
