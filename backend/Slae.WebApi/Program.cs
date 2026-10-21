using Microsoft.EntityFrameworkCore;
using Slae.Persistence;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString(nameof(SlaeDbContext))
    ?? throw new InvalidOperationException(
    $"Connection string '{nameof(SlaeDbContext)}' not found. Check user-secrets settings"
    );

builder.Services.AddPersistence(connectionString);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
