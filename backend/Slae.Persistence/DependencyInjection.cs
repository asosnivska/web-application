using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Slae.Application.Interfaces;
using Slae.Persistence.Services;


namespace Slae.Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<SlaeDbContext>(o => o.UseNpgsql(connectionString));
            services.AddScoped<ITaskStore, TaskStore>();

            services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            services.AddSingleton<ITaskQueue, TaskQueue>();
            return services;
        }
    }
}
