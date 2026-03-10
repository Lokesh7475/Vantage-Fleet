using StackExchange.Redis;
using StateService.Services;
using StateService.Workers;
using StateService.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<StateService.Workers.KafkaConsumerWorker>();

// 1. Add SignalR Services
builder.Services.AddSignalR();

var frontendUrls = Environment.GetEnvironmentVariable("FRONTEND_URL")?.Split(',') ?? new[] { "http://localhost:4200" };

// 2. Configure CORS so Angular can connect
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", builder =>
    {
        builder.WithOrigins(frontendUrls)
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials(); // Crucial for SignalR WebSockets!
    });
});

// 1. Connect to Redis (safeguard against Docker startup races)
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost:6379";
var redisConnectionString = $"{redisHost},abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnectionString));

// 2. Register our custom Redis Service
builder.Services.AddSingleton<StateService.Services.IRedisService, StateService.Services.RedisService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 3. Use CORS before UseAuthorization
app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
