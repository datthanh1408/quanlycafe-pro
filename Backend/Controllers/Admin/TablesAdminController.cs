using CoffeeShop.Areas.Admin.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class TablesAdminController : Controller
{
    private static readonly HashSet<string> ValidZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tầng 1", "Tầng 2", "Sân vườn"
    };

    private readonly AppDbContext _db;
    private readonly ITableService _tableService;
    private readonly IAuditService _auditService;

    public TablesAdminController(AppDbContext db, ITableService tableService, IAuditService auditService)
    {
        _db = db;
        _tableService = tableService;
        _auditService = auditService;
    }

    [HttpGet("/admin/tables")]
    public async Task<IActionResult> Index(string? zone = null, int? editId = null)
    {
        var query = _db.CafeTables
            .AsNoTracking()
            .Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(zone))
        {
            query = query.Where(t => t.Zone == zone);
        }

        var tables = await query
            .OrderBy(t => t.Zone)
            .ThenBy(t => t.Code)
            .ToListAsync();

        var openOrderTableIds = await _db.Orders
            .AsNoTracking()
            .Where(o => o.TableId.HasValue &&
                        o.Status != OrderStatus.Completed &&
                        o.Status != OrderStatus.Cancelled)
            .Select(o => o.TableId!.Value)
            .Distinct()
            .ToListAsync();

        var tables3D = await _tableService.GetTablesFor3DAsync(null);

        var form = new CafeTableFormModel
        {
            Zone = "Tầng 1",
            Capacity = 4,
            PosX = -2.4,
            PosY = 0.0,
            IsActive = true
        };

        if (editId.HasValue)
        {
            var editTarget = await _db.CafeTables.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == editId.Value && !t.IsDeleted);
            if (editTarget != null)
            {
                form = new CafeTableFormModel
                {
                    Id = editTarget.Id,
                    Code = editTarget.Code,
                    Zone = editTarget.Zone,
                    Capacity = editTarget.Capacity,
                    PosX = editTarget.PosX,
                    PosY = editTarget.PosY,
                    IsActive = editTarget.IsActive
                };
            }
        }

        var vm = new TablesAdminViewModel
        {
            ZoneFilter = zone,
            Tables = tables,
            Tables3D = tables3D,
            TableIdsWithOpenOrder = openOrderTableIds.ToHashSet(),
            Form = form
        };

        return View(vm);
    }

    [HttpPost("/admin/tables/update-position")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePosition([FromBody] TablePositionUpdateRequest? req)
    {
        if (req == null || req.Id <= 0)
        {
            return BadRequest(new { success = false, message = "Dữ liệu tọa độ bàn không hợp lệ." });
        }

        var normalizedZone = NormalizeZone(req.Zone);
        var (ok, err) = await _tableService.UpdateTablePosition3DAsync(req.Id, req.PosX, req.PosY, normalizedZone);
        if (!ok)
        {
            return BadRequest(new { success = false, message = err ?? "Không thể lưu vị trí bàn 3D." });
        }

        await _auditService.LogAsync("MoveTable3D", "CafeTable", req.Id.ToString(), null,
            new { req.PosX, req.PosY, Zone = normalizedZone });

        return Ok(new
        {
            success = true,
            id = req.Id,
            posX = Math.Round(req.PosX, 2),
            posY = Math.Round(req.PosY, 2),
            zone = normalizedZone,
            message = $"Đã lưu tọa độ 3D ({req.PosX:0.00}, {req.PosY:0.00}) • {normalizedZone}"
        });
    }

    [HttpPost("/admin/tables/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CafeTableFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Code))
        {
            TempData["ToastError"] = "Vui lòng nhập mã bàn.";
            return RedirectToAction(nameof(Index));
        }

        var cleanCode = form.Code.Trim().ToUpperInvariant();
        var cleanZone = NormalizeZone(form.Zone) ?? "Tầng 1";
        var capacity = Math.Clamp(form.Capacity, 1, 20);
        var posX = Math.Round(Math.Clamp(form.PosX, -12.0, 12.0), 2);
        var posY = Math.Round(Math.Clamp(form.PosY, -12.0, 12.0), 2);

        if (form.Id == 0)
        {
            var existingAny = await _db.CafeTables.FirstOrDefaultAsync(t => t.Code == cleanCode);
            if (existingAny != null && !existingAny.IsDeleted)
            {
                TempData["ToastError"] = $"Mã bàn '{cleanCode}' đã tồn tại. Vui lòng chọn mã bàn khác.";
                return RedirectToAction(nameof(Index));
            }

            if (existingAny != null && existingAny.IsDeleted)
            {
                // Khôi phục bản ghi đã xóa mềm có cùng mã để tránh đụng Unique Index
                existingAny.IsDeleted = false;
                existingAny.Zone = cleanZone;
                existingAny.Capacity = capacity;
                existingAny.PosX = posX;
                existingAny.PosY = posY;
                existingAny.IsActive = form.IsActive;
                existingAny.Status = TableStatus.Available;
                await _db.SaveChangesAsync();

                await _auditService.LogAsync("RestoreCreateTable", "CafeTable", existingAny.Id.ToString(), null,
                    new { existingAny.Code, existingAny.Zone, existingAny.Capacity, existingAny.PosX, existingAny.PosY });

                TempData["ToastSuccess"] = $"Đã tạo bàn '{existingAny.Code}' ({existingAny.Zone}).";
                return RedirectToAction(nameof(Index));
            }

            var tbl = new CafeTable
            {
                Code = cleanCode,
                Zone = cleanZone,
                Capacity = capacity,
                PosX = posX,
                PosY = posY,
                IsActive = form.IsActive,
                IsDeleted = false,
                Status = TableStatus.Available
            };

            _db.CafeTables.Add(tbl);
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("CreateTable", "CafeTable", tbl.Id.ToString(), null,
                new { tbl.Code, tbl.Zone, tbl.Capacity, tbl.PosX, tbl.PosY, tbl.IsActive });

            TempData["ToastSuccess"] = $"Đã thêm bàn mới '{tbl.Code}' vào {tbl.Zone}.";
        }
        else
        {
            var tbl = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == form.Id && !t.IsDeleted);
            if (tbl == null)
            {
                TempData["ToastError"] = "Không tìm thấy bàn cần cập nhật.";
                return RedirectToAction(nameof(Index));
            }

            var codeConflict = await _db.CafeTables.AnyAsync(t => t.Code == cleanCode && t.Id != tbl.Id);
            if (codeConflict)
            {
                TempData["ToastError"] = $"Mã bàn '{cleanCode}' đã được sử dụng cho bàn khác.";
                return RedirectToAction(nameof(Index), new { editId = tbl.Id });
            }

            if (!form.IsActive)
            {
                var hasOpen = await HasOpenOrderAsync(tbl.Id);
                if (hasOpen)
                {
                    TempData["ToastError"] = $"Bàn '{tbl.Code}' đang có đơn mở phục vụ khách, không thể tạm ngưng.";
                    return RedirectToAction(nameof(Index), new { editId = tbl.Id });
                }
            }

            var oldSnap = new { tbl.Code, tbl.Zone, tbl.Capacity, tbl.PosX, tbl.PosY, tbl.IsActive };
            tbl.Code = cleanCode;
            tbl.Zone = cleanZone;
            tbl.Capacity = capacity;
            tbl.PosX = posX;
            tbl.PosY = posY;
            tbl.IsActive = form.IsActive;

            await _db.SaveChangesAsync();

            await _auditService.LogAsync("UpdateTable", "CafeTable", tbl.Id.ToString(), oldSnap,
                new { tbl.Code, tbl.Zone, tbl.Capacity, tbl.PosX, tbl.PosY, tbl.IsActive });

            TempData["ToastSuccess"] = $"Đã lưu cập nhật bàn '{tbl.Code}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/tables/toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var tbl = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (tbl == null)
        {
            TempData["ToastError"] = "Không tìm thấy bàn.";
            return RedirectToAction(nameof(Index));
        }

        if (tbl.IsActive && await HasOpenOrderAsync(tbl.Id))
        {
            TempData["ToastError"] = $"Bàn '{tbl.Code}' đang có đơn hàng chưa hoàn tất, không thể tạm ngưng.";
            return RedirectToAction(nameof(Index));
        }

        tbl.IsActive = !tbl.IsActive;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("ToggleTable", "CafeTable", tbl.Id.ToString(),
            new { IsActive = !tbl.IsActive }, new { tbl.IsActive });

        TempData["ToastSuccess"] = $"Bàn '{tbl.Code}' đã chuyển sang {(tbl.IsActive ? "Hoạt động" : "Tạm ngưng")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/tables/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var tbl = await _db.CafeTables.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (tbl == null)
        {
            TempData["ToastError"] = "Không tìm thấy bàn.";
            return RedirectToAction(nameof(Index));
        }

        // CHẶN xóa bàn đang có đơn mở
        if (await HasOpenOrderAsync(tbl.Id) || tbl.Status == TableStatus.Occupied)
        {
            TempData["ToastError"] = $"Không thể xóa bàn '{tbl.Code}' vì bàn đang có khách / đơn hàng mở chưa hoàn tất.";
            return RedirectToAction(nameof(Index));
        }

        tbl.IsDeleted = true;
        tbl.IsActive = false;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("DeleteTable", "CafeTable", tbl.Id.ToString(),
            new { tbl.Code, tbl.Zone }, new { IsDeleted = true });

        TempData["ToastSuccess"] = $"Đã xóa bàn '{tbl.Code}' khỏi sơ đồ quán.";
        return RedirectToAction(nameof(Index));
    }

    private Task<bool> HasOpenOrderAsync(int tableId)
    {
        return _db.Orders.AnyAsync(o =>
            o.TableId == tableId &&
            o.Status != OrderStatus.Completed &&
            o.Status != OrderStatus.Cancelled);
    }

    private static string? NormalizeZone(string? zone)
    {
        if (string.IsNullOrWhiteSpace(zone)) return null;
        var trimmed = zone.Trim();
        foreach (var vz in ValidZones)
        {
            if (string.Equals(vz, trimmed, StringComparison.OrdinalIgnoreCase))
                return vz;
        }
        return "Tầng 1";
    }
}
