using Confluent.Kafka;
using IngestionService.Models;
using System.Text.Json;

namespace IngestionService.Services
{
    public class TelemetryKafkaProducer : IKafkaProducer
    {
        private readonly IProducer<Null, string> _producer;
        private readonly string _topic = "vehicle-telemetry";

        public TelemetryKafkaProducer(IConfiguration config)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BROKER") ?? config["Kafka:BootstrapServers"] ?? "localhost:9092"
            };

            _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        }

        public async Task ProduceTelemetryAsync(VehicleTelemetryDto payload)
        {
            var message = new Message<Null, string>
            {
                Value = JsonSerializer.Serialize(payload)
            };

            await _producer.ProduceAsync(_topic, message);
        }
    }
}