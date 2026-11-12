using Slae.Persistence;
using Slae.Worker;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SlaeDbContext")
    ?? throw new InvalidOperationException(
        "Connection string 'SlaeDbContext' not found. Check user-secrets settings");

string serverId = builder.Configuration["ServerId"] ?? $"{Environment.MachineName}-{Environment.ProcessId}";

builder.Services.AddPersistence(connectionString);
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.AddSingleton(new WorkerIdentity(serverId));
builder.Services.AddSingleton<TaskRunner>();

builder.Services.AddHostedService<SolverWorker>();
builder.Services.AddHostedService<StaleRecoveryService>();

await builder.Build().RunAsync();
