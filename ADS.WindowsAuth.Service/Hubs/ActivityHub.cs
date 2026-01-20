using Microsoft.AspNetCore.SignalR;

namespace ADS.WindowsAuth.Service.Hubs;

/// <summary>
/// SignalR Hub за real-time обновления на активност
/// </summary>
public class ActivityHub : Hub
{
    public async Task JoinMachineGroup(string machineName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"machine_{machineName}");
    }

    public async Task LeaveMachineGroup(string machineName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"machine_{machineName}");
    }
}

