using StackExchange.Redis;
using System.Text.Json;
using StateService.Models;

namespace StateService.Services
{
    public class RedisService : IRedisService
    {
        private readonly IDatabase _db;

        // IConnectionMultiplexer is the object StackExchange.Redis uses to keep the connection open
        public RedisService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task SetVehicleStateAsync(VehicleTelemetryDto telemetry)
        {
            var json = JsonSerializer.Serialize(telemetry);
            
            // We use the prefix "vehicle:" to keep our Redis database organized
            // We overwrite whatever was there before, so this always holds the newest location
            await _db.StringSetAsync($"vehicle:{telemetry.VehicleId}", json);
        }

        public async Task<VehicleTelemetryDto?> GetVehicleStateAsync(string vehicleId)
        {
            var json = await _db.StringGetAsync($"vehicle:{vehicleId}");
            
            if (json.IsNullOrEmpty) return null;
            
            return JsonSerializer.Deserialize<VehicleTelemetryDto>(json.ToString());
        }
    }
}