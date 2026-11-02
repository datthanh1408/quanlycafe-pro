using CoffeeShop.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoffeeShop.Hubs;

public class OrderHub : Hub
{
    public const string StaffGroup = "StaffAndAdmin";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(AppRoles.Staff) == true ||
            Context.User?.IsInRole(AppRoles.Admin) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinOrderGroup(string orderCode)
    {
        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Order_{orderCode.Trim().ToUpperInvariant()}");
        }
    }

    public async Task JoinFloorPlanGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "FloorPlanViewers");
    }
}

