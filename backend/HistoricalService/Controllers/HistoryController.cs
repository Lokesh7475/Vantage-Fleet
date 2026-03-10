using HistoricalService.Data;
using HistoricalService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching; // Add this using statement
using Microsoft.EntityFrameworkCore;

namespace HistoricalService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistoryController : ControllerBase
    {
        private readonly HistoryDbContext _context;

        public HistoryController(HistoryDbContext context)
        {
            _context = context;
        }

        // Cache this for 60 seconds. 
        [HttpGet("vehicles")]
        [OutputCache(Duration = 60)] 
        public async Task<IActionResult> GetAllVehicles()
        {
            var vehicles = await _context.Vehicles
                .Select(v => new { v.Id, v.Description })
                .ToListAsync();

            return Ok(vehicles);
        }

        // Cache this for 15 seconds.
        // VaryByRouteValue means it creates a separate cache for SIM-01 and SIM-02!
        [HttpGet("vehicle/{vehicleId}/trips")]
        [OutputCache(Duration = 15, VaryByRouteValueNames = new[] { "vehicleId" })] 
        public async Task<IActionResult> GetVehicleTrips(string vehicleId)
        {
            var trips = await _context.Trips
                .Where(t => t.VehicleId == vehicleId)
                .OrderByDescending(t => t.StartTime) 
                .Select(t => new { t.Id, t.VehicleId, t.StartTime, t.EndTime, t.StartLatitude, t.StartLongitude, t.EndLatitude, t.EndLongitude })
                .ToListAsync();

            return Ok(trips);
        }

        // Cache this for 5 seconds to protect the DB from heavy coordinate queries
        [HttpGet("trip/{tripId}/route")]
        [OutputCache(Duration = 5, VaryByRouteValueNames = new[] { "tripId" })]
        public async Task<IActionResult> GetTripRoute(Guid tripId)
        {
            var points = await _context.TelemetryRecords
                .Where(r => r.TripId == tripId)
                .OrderBy(r => r.Timestamp) 
                .Select(r => new RoutePointDto
                {
                    Latitude = r.Location.Y,  
                    Longitude = r.Location.X, 
                    Speed = r.Speed,
                    Timestamp = r.Timestamp
                })
                .ToListAsync();

            if (!points.Any()) return NotFound("No route found for this trip.");

            return Ok(points);
        }
    }
}