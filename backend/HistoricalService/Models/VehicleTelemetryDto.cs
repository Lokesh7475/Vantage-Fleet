namespace HistoricalService.Models
{
    public class VehicleTelemetryDto
    {
        public Guid TripId { get; set; }
        public string VehicleId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; }
        public DateTime Timestamp { get; set; }
    }
}