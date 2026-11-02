using CoffeeShop.Data;
using CoffeeShop.Models.Enums;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Controllers;

[ApiController]
public class RealtimeApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITableService _tableService;

    public RealtimeApiController(AppDbContext db, ITableService tableService)
    {
        _db = db;
        _tableService = tableService;
    }

    [HttpGet("/api/realtime/ping")]
    [AllowAnonymous]
    public async Task<IActionResult> Ping()
    {
        var pendingCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
        var latestUpdate = await _db.Orders
            .OrderByDescending(o => o.UpdatedAt)
            .Select(o => (DateTime?)o.UpdatedAt)
            .FirstOrDefaultAsync();

        var marker = $"{pendingCount}_{latestUpdate?.Ticks ?? 0}";
        return Ok(new { pendingCount, marker });
    }

    [HttpGet("/api/tables/3d")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTables3D([FromQuery] string? zone = null)
    {
        var list = await _tableService.GetTablesFor3DAsync(zone);
        // Nếu người gọi không phải Staff/Admin thì ẩn mã đơn và tên khách đặt trước của bàn khác để bảo mật riêng tư
        bool isStaffOrAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");
        if (!isStaffOrAdmin)
        {
            foreach (var t in list)
            {
                t.ActiveOrderId = null;
                t.ActiveOrderCode = null;
                t.ActiveOrderTotal = null;
                t.UpcomingReservationInfo = t.Status == TableStatus.Reserved ? "Đã có lịch đặt trước" : null;
            }
        }

        return Ok(list);
    }
}

