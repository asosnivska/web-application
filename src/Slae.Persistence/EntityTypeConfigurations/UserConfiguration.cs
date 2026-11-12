using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Slae.Domain.Entities;

namespace Slae.Persistence.EntityTypeConfigurations
{
    internal class UserConfiguration : IEntityTypeConfiguration<User>
    {
        private const string ColUsername = "\"Username\"";
        private const string ColMaxTasks = "\"MaxConcurrentTasks\"";

        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users", t =>
            {
                t.HasCheckConstraint("CK_Users_MaxConcurrentTasks_Positive", 
                    $"{ColMaxTasks} >= 1");

                t.HasCheckConstraint("CK_Users_Username_NotBlank", 
                    $"char_length(btrim({ColUsername})) > 0");
            });

            builder.HasKey(u => u.Id);

            builder.Property(u => u.Id).ValueGeneratedNever();

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("UX_Users_Username");

            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);
        }
    }
}
