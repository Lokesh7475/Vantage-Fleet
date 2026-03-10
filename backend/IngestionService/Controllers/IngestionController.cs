using Microsoft.AspNetCore.Mvc;
using IngestionService.Models;
using IngestionService.Services;

namespace IngestionService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IngestionController : ControllerBase
    {
        private readonly ILogger<IngestionController> _logger;
        private readonly IKafkaProducer _kafkaProducer;

        public IngestionController(ILogger<IngestionController> logger, IKafkaProducer kafkaProducer)
        {
            _logger = logger;
            _kafkaProducer = kafkaProducer;
        }

        [HttpPost("telemetry")]
        public async Task<IActionResult> ReceiveTelemetry([FromBody] VehicleTelemetryDto payload)
        {

            await _kafkaProducer.ProduceTelemetryAsync(payload);

            // Logging to prove it hit the API
            _logger.LogInformation("Received ping from {VehicleId} at [{Lat}, {Lng}]", 
                payload.VehicleId, payload.Latitude, payload.Longitude);

            // In the next step, we will add: _kafkaProducer.Publish(payload);

            // Return 202 Accepted immediately. Ingestion APIs should never wait!
            return Accepted();
        }
    } 
}