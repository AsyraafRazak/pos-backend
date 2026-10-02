using Microsoft.AspNetCore.SignalR;
using pos_backend.DTOs;

namespace pos_backend.Hubs;

public interface IPosClient
{
    Task OrderCreated(OrderResponseDto order);
    Task OrderFiredToKitchen(OrderResponseDto order);
    Task OrderStatusChanged(OrderResponseDto order);
    Task OrderPaid(OrderResponseDto order);
    Task TableStatusChanged(TableDto table);
    Task ReceiveMessage(string user, string message);
}

public class PosHub : Hub<IPosClient>
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
