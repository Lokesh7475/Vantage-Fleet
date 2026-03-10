// /Models/Trip.cs
namespace HistoricalService.Models
{
    public class Trip
    {
        public Guid Id { get; set; } 
        
        // 1. Foreign Key mapping back to the Vehicles table
        public required string VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; } 
        
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; } 

        // 2. New Start and End Coordinates
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        
        // Nullable because the trip might currently be in progress!
        public double? EndLatitude { get; set; }
        public double? EndLongitude { get; set; }

        public ICollection<VehicleTelemetryRecord> RoutePoints { get; set; } = new List<VehicleTelemetryRecord>();
    }
}