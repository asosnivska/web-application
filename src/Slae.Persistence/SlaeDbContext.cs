using Microsoft.EntityFrameworkCore;
using Slae.Application.Interfaces;
using Slae.Domain.Entities;
using Slae.Persistence.EntityTypeConfigurations;

namespace Slae.Persistence
{
    public class SlaeDbContext : DbContext, ISlaeDbContext
    {
        public SlaeDbContext(DbContextOptions<SlaeDbContext> options) : base(options) { }

        public DbSet<SlaeTask> Tasks { get; set; }
        public DbSet<User> Users {  get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SlaeTaskConfiguration());
            modelBuilder.ApplyConfiguration(new UserConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}
