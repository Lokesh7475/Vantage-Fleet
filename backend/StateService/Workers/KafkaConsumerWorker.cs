using Confluent.Kafka;
using System.Text.Json;
using StateService.Models;
using StateService.Services;
using Microsoft.AspNetCore.SignalR; // Add this
using StateService.Hubs;          // Add this

namespace StateService.Workers
{
    public class KafkaConsumerWorker : BackgroundService
    {
        private readonly ILogger<KafkaConsumerWorker> _logger;
        private readonly IRedisService _redis;
        private readonly IHubContext<TelemetryHub> _hubContext; // Add this
        private readonly string _topic = "vehicle-telemetry";
        private readonly IConsumer<Ignore, string> _consumer;

        // Inject the Hub Context here
        public KafkaConsumerWorker(
            ILogger<KafkaConsumerWorker> logger, 
            IConfiguration config, 
            IRedisService redis,
            IHubContext<TelemetryHub> hubContext) 
        {
            _logger = logger;
            _redis = redis;
            _hubContext = hubContext;

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BROKER") ?? config["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = "state-service-group",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumingAsync(stoppingToken), stoppingToken);
        }

        private async Task StartConsumingAsync(CancellationToken stoppingToken)
        {
            _consumer.Subscribe(_topic);
            _logger.LogInformation("Kafka Consumer Worker started. Listening to topic: {Topic}", _topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(stoppingToken);
                        var rawJson = consumeResult.Message.Value;
                        var telemetry = JsonSerializer.Deserialize<VehicleTelemetryDto>(rawJson);

                        if (telemetry != null)
                        {
                            // 1. Save to Redis
                            await _redis.SetVehicleStateAsync(telemetry);
                            
                            // 2. Broadcast to Angular
                            await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", telemetry, stoppingToken);
                            
                            _logger.LogInformation("Broadcasted {VehicleId} to Angular", telemetry.VehicleId);
                        }
                    }
                    catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        // Gracefully wait for the topic to be created
                        _logger.LogWarning("Topic '{Topic}' not found yet. Waiting for Ingestion Service...", _topic);
                        await Task.Delay(3000, stoppingToken); 
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Kafka Consumer was gracefully cancelled.");
            }
            finally
            {
                _consumer.Close();
            }
        }
    }
}