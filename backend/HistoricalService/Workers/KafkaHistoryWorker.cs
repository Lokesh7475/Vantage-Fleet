using Confluent.Kafka;
using HistoricalService.Data;
using HistoricalService.Models;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace HistoricalService.Workers
{
    public class KafkaHistoryWorker : BackgroundService
    {
        private readonly ILogger<KafkaHistoryWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _topic = "vehicle-telemetry";

        public KafkaHistoryWorker(ILogger<KafkaHistoryWorker> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory; // We use this to safely get our DbContext
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumingAsync(stoppingToken), stoppingToken);
        }

        private async Task StartConsumingAsync(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BROKER") ?? "localhost:9092",
                GroupId = "historical-service-group",
                AutoOffsetReset = AutoOffsetReset.Earliest, // Never miss a point!
                EnableAutoCommit = true
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(_topic);
            _logger.LogInformation("Historical Worker listening to {Topic}...", _topic);

            var batch = new List<VehicleTelemetryRecord>();

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try 
                    {
                        var result = consumer.Consume(stoppingToken);
                        var dto = JsonSerializer.Deserialize<VehicleTelemetryDto>(result.Message.Value);

                        if (dto != null)
                        {
                            var record = new VehicleTelemetryRecord
                            {
                                TripId = dto.TripId,
                                VehicleId = dto.VehicleId,
                                Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 },
                                Speed = dto.Speed,
                                Heading = dto.Heading,
                                Timestamp = dto.Timestamp
                            };

                            batch.Add(record);

                            if (batch.Count >= 10)
                            {
                                await SaveBatchAsync(batch);
                                batch.Clear();
                            }
                        }
                    }
                    catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        // If the topic doesn't exist yet, just wait 3 seconds and try again!
                        _logger.LogWarning("Topic '{Topic}' not found. Waiting for Ingestion Service to create it...", _topic);
                        Thread.Sleep(3000); 
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally { consumer.Close(); }
        }

        private async Task SaveBatchAsync(List<VehicleTelemetryRecord> records)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();

            // 1. Ensure the Vehicles exist before saving Trips (Foreign Key constraint)
            var vehicleIds = records.Select(r => r.VehicleId).Distinct().ToList();
            var existingVehicles = db.Vehicles.Where(v => vehicleIds.Contains(v.Id)).Select(v => v.Id).ToList();
            
            foreach (var vId in vehicleIds.Except(existingVehicles))
            {
                db.Vehicles.Add(new Vehicle { Id = vId, Description = "Auto-registered from telemetry stream" });
                _logger.LogInformation("Registered new Vehicle: {VehicleId}", vId);
            }
            await db.SaveChangesAsync(); // Save vehicles immediately so the DB generates them

            // 2. Handle Trips (Create new ones, update End Coordinates for existing ones)
            var tripIds = records.Select(r => r.TripId).Distinct().ToList();
            var existingTrips = db.Trips.Where(t => tripIds.Contains(t.Id)).ToList();
            var existingTripIds = existingTrips.Select(t => t.Id).ToList();

            foreach (var tripId in tripIds)
            {
                // Get all points in this batch for this specific trip, sorted by time
                var tripPoints = records.Where(r => r.TripId == tripId).OrderBy(r => r.Timestamp).ToList();
                var firstPoint = tripPoints.First();
                var lastPoint = tripPoints.Last();

                if (!existingTripIds.Contains(tripId))
                {
                    // It's a brand new trip! Set the starting location.
                    var newTrip = new Trip 
                    { 
                        Id = tripId, 
                        VehicleId = firstPoint.VehicleId, 
                        StartTime = firstPoint.Timestamp,
                        StartLatitude = firstPoint.Location.Y,
                        StartLongitude = firstPoint.Location.X,
                        // Immediately set the end to the newest point we have so far
                        EndTime = lastPoint.Timestamp,
                        EndLatitude = lastPoint.Location.Y,
                        EndLongitude = lastPoint.Location.X
                    };
                    db.Trips.Add(newTrip);
                    existingTrips.Add(newTrip); // Add to our working list so it can be updated in future batches
                    _logger.LogInformation("Created new Trip {TripId} starting at [{Lat}, {Lng}]", tripId, newTrip.StartLatitude, newTrip.StartLongitude);
                }
                else
                {
                    // The trip already exists. We just push the "End" marker further down the road.
                    var existingTrip = existingTrips.First(t => t.Id == tripId);
                    if (existingTrip.EndTime == null || lastPoint.Timestamp > existingTrip.EndTime)
                    {
                        existingTrip.EndTime = lastPoint.Timestamp;
                        existingTrip.EndLatitude = lastPoint.Location.Y;
                        existingTrip.EndLongitude = lastPoint.Location.X;
                    }
                }
            }

            // 3. Save the actual breadcrumbs
            db.TelemetryRecords.AddRange(records);
            await db.SaveChangesAsync();
            
            _logger.LogInformation("Processed batch of {Count} coordinates.", records.Count);
        }
    }
}