using NetTopologySuite.Geometries;

namespace HistoricalService.Models
{
    public class VehicleTelemetryRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TripId { get; set; }
        public Trip? Trip { get; set; } 
        public required string VehicleId { get; set; }
        public required Point Location { get; set; } 
        public double Speed { get; set; }
        public double Heading { get; set; }
        public DateTime Timestamp { get; set; } 
    }
}