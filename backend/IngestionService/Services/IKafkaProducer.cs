using IngestionService.Models;

namespace IngestionService.Services
{
    public interface IKafkaProducer
    {
        Task ProduceTelemetryAsync(VehicleTelemetryDto payload);
    }
}