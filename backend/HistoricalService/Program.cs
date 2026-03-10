using HistoricalService.Workers;
using Microsoft.EntityFrameworkCore;
using HistoricalService.Data;
using DotNetEnv;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. ADD THIS LINE RIGHT HERE:
builder.Services.AddOutputCache();

var frontendUrls = Environment.GetEnvironmentVariable("FRONTEND_URL")?.Split(',') ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", builder =>
    {
        builder.WithOrigins(frontendUrls)
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var connectionString = $"Host={Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost"};" +
                       $"Port={Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5433"};" +
                       $"Database={Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "fleet_history"};" +
                       $"Username={Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "fleet_admin"};" +
                       $"Password={Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "supersecretpassword"}";

builder.Services.AddDbContext<HistoryDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        o => o.UseNetTopologySuite() // This is the magic line for map coordinates!
    ));

builder.Services.AddHostedService<KafkaHistoryWorker>();

var app = builder.Build();

// Auto-apply any pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

// 2. ADD THIS: Enable the caching middleware (Must be BEFORE MapControllers!)
app.UseOutputCache();

app.UseAuthorization();
app.MapControllers();

app.Run();