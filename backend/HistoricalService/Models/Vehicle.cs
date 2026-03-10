// /Models/Vehicle.cs
namespace HistoricalService.Models
{
    public class Vehicle
    {
        // This will be "SIM-01-AHD"
        public required string Id { get; set; } 
        
        // You can add more vehicle metadata here later (e.g., DriverName, LicensePlate)
        public string? Description { get; set; }

        // Navigation Property: One Vehicle has Many Trips
        public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    }
}