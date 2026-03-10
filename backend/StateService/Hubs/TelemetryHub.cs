// /Hubs/TelemetryHub.cs
using Microsoft.AspNetCore.SignalR;

namespace StateService.Hubs
{
    public class TelemetryHub : Hub
    {
        // When your Angular app connects, this method will fire automatically
        public override Task OnConnectedAsync()
        {
            Console.WriteLine($"[SignalR] Frontend connected! ID: {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] Frontend disconnected. ID: {Context.ConnectionId}");
            return base.OnDisconnectedAsync(exception);
        }
    }
}