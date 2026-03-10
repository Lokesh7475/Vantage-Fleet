using StateService.Models;

namespace StateService.Services
{
    public interface IRedisService
    {
        Task SetVehicleStateAsync(VehicleTelemetryDto telemetry);
        Task<VehicleTelemetryDto?> GetVehicleStateAsync(string vehicleId);
    }
}