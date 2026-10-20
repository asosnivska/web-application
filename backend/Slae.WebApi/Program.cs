using Microsoft.EntityFrameworkCore;
using Slae.Persistence;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddDbContext<SlaeDbContext>(
    options =>
    {
        var connectionString = configuration.GetConnectionString(nameof(SlaeDbContext))
            ?? throw new InvalidOperationException(
            $"Connection string '{nameof(SlaeDbContext)}' not found. Check user-secrets settings"
            );

        options.UseNpgsql(connectionString);
    });

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
