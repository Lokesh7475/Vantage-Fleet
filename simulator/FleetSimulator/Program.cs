using System.Net.Http.Json;
using System.Text.Json;
using DotNetEnv;

Env.TraversePath().Load();

var baseIngestionUrl = Environment.GetEnvironmentVariable("INGESTION_SERVICE_URL") ?? "http://localhost:5195";
var ingestionApiUrl = $"{baseIngestionUrl}/api/ingestion/telemetry";
using var httpClient = new HttpClient();

// 1. Define a pool of coordinates in Ahmedabad (Longitude,Latitude)
var locations = new List<string>
{
    "72.5714,23.0225", // Navrangpura
    "72.5293,23.0338", // Vastrapur
    "72.5833,23.0233", // Manek Chowk
    "72.5000,23.0500", // SG Highway
    "72.5800,23.0600", // Ashram
    "72.6000,23.0000", // Kankaria
    "72.5400,23.0100", // Paldi
    "72.6800,23.0300"  // Odhav
};

var fleet = new List<SimulatedVehicle>();
var usedRoutes = new HashSet<string>();
var random = new Random();

// Set how many trucks you want to simulate
int numberOfVehicles = 3; 

Console.WriteLine($"Generating {numberOfVehicles} unique routes...");

// 2. Assign unique Start and End points for each vehicle
for (int i = 1; i <= numberOfVehicles; i++)
{
    string startCoords;
    string endCoords;
    string routeKey;

    // Keep picking random points until we find a pair that aren't identical, 
    // and haven't been assigned to another truck yet.
    do
    {
        startCoords = locations[random.Next(locations.Count)];
        endCoords = locations[random.Next(locations.Count)];
        
        // Create a unique signature for this route (e.g., "start|end")
        routeKey = $"{startCoords}|{endCoords}";
    } 
    while (startCoords == endCoords || usedRoutes.Contains(routeKey));

    // Lock in the route so no other truck can take it
    usedRoutes.Add(routeKey);
    
    // Automatically name them SIM-01-AHD, SIM-02-AHD, etc.
    string vehicleId = $"SIM-{i:D2}-AHD"; 
    
    fleet.Add(new SimulatedVehicle(vehicleId, startCoords, endCoords, httpClient, ingestionApiUrl));
}

Console.WriteLine($"Starting simulation for {fleet.Count} vehicles...");

var drivingTasks = fleet.Select(v => v.StartDrivingAsync());
await Task.WhenAll(drivingTasks);

Console.WriteLine("All vehicles have reached their destinations!");

public class SimulatedVehicle{
    private readonly string _vehicleId;
    private readonly string _startCoords;
    private readonly string _endCoords;
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;

    public SimulatedVehicle(string id, string start, string end, HttpClient client, string apiUrl){
        _vehicleId = id;
        _startCoords = start;
        _endCoords = end;
        _httpClient = client;
        _apiUrl = apiUrl;
    }

    public async Task StartDrivingAsync()
    {
        var osrmUrl = $"http://router.project-osrm.org/route/v1/driving/{_startCoords};{_endCoords}?overview=full&geometries=geojson";
        
        try 
        {
            var response = await _httpClient.GetAsync(osrmUrl);
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            var coordinates = document.RootElement
                .GetProperty("routes")[0]
                .GetProperty("geometry")
                .GetProperty("coordinates")
                .EnumerateArray()
                .ToList();

            // 1. GENERATE THE TRIP ID HERE (One ID per route)
            var currentTripId = Guid.NewGuid();

            Console.WriteLine($"[{_vehicleId}] Starting Trip {currentTripId}. Driving {coordinates.Count} waypoints...");

            double currentHeading = 0;
            double currentSpeed = 0;
            double? prevLat = null;
            double? prevLng = null;
            var lastPingTime = DateTime.UtcNow;

            foreach (var coord in coordinates)
            {
                double currentLng = coord[0].GetDouble();
                double currentLat = coord[1].GetDouble();
                var now = DateTime.UtcNow;

                if (prevLat.HasValue && prevLng.HasValue)
                {
                    double rLat1 = prevLat.Value * Math.PI / 180.0;
                    double rLat2 = currentLat * Math.PI / 180.0;
                    double dLon = (currentLng - prevLng.Value) * Math.PI / 180.0;
                    double dLat = rLat2 - rLat1;

                    // Calculate Heading (Bearing)
                    double y = Math.Sin(dLon) * Math.Cos(rLat2);
                    double x = Math.Cos(rLat1) * Math.Sin(rLat2) - Math.Sin(rLat1) * Math.Cos(rLat2) * Math.Cos(dLon);
                    currentHeading = (Math.Atan2(y, x) * 180.0 / Math.PI + 360) % 360;

                    // Calculate Distance using Haversine formula (in km)
                    double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + 
                               Math.Cos(rLat1) * Math.Cos(rLat2) * 
                               Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                    double distanceKm = 6371.0 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

                    // To prevent wild UI fluctuations, we abandon raw generic distance/time logic 
                    // (since OSRM points are spaced randomly by road curves, not time)
                    // and instead simulate a smooth cruising speed (35-45 km/h) with acceleration.
                    double targetSpeed = 40.0 + (Random.Shared.NextDouble() * 10 - 5); 
                    
                    // Exponential Moving Average to prevent sudden jumps
                    if (currentSpeed == 0) currentSpeed = 10; // Initial push to start moving
                    currentSpeed = (currentSpeed * 0.8) + (targetSpeed * 0.2);
                }

                prevLat = currentLat;
                prevLng = currentLng;
                lastPingTime = now;

                var payload = new 
                {
                    TripId = currentTripId, // 2. ATTACH IT TO EVERY PING
                    VehicleId = _vehicleId,
                    Latitude = currentLat,
                    Longitude = currentLng,
                    Speed = Math.Round(currentSpeed),
                    Heading = Math.Round(currentHeading),
                    Timestamp = now
                };

                await _httpClient.PostAsJsonAsync(_apiUrl, payload);
                Console.WriteLine($"[{_vehicleId}] Pinged API at [{currentLat:0.0000}, {currentLng:0.0000}] | {Math.Round(currentSpeed)} km/h | {Math.Round(currentHeading)} DEG");

                await Task.Delay(1000); 
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_vehicleId}] Failed to drive: {ex.Message}");
        }
    }
}