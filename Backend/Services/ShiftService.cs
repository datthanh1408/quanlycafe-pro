using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public interface IShiftService
{
    Task<Shift?> GetActiveShiftAsync(string staffId);
    Task<(bool Success, Shift? Shift, string? ErrorMessage)> OpenShiftAsync(string staffId, decimal openingCash, string? note);
    Task<(bool Success, Shift? Shift, string? ErrorMessage)> CloseShiftAsync(string staffId, decimal countedCash, string? note);
}

public class ShiftService : IShiftService
{
    private readonly AppDbContext _db;

    public ShiftService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Shift?> GetActiveShiftAsync(string staffId)
    {
        return await _db.Shifts
            .Include(s => s.Staff)
            .FirstOrDefaultAsync(s => s.StaffId == staffId && s.ClosedAt == null);
    }

    public async Task<(bool Success, Shift? Shift, string? ErrorMessage)> OpenShiftAsync(string staffId, decimal openingCash, string? note)
    {
        if (openingCash < 0)
            return (false, null, "Tiền đầu ca không được âm.");

        var active = await GetActiveShiftAsync(staffId);
        if (active != null)
            return (false, active, "Bạn đang có một ca làm việc chưa đóng.");

        var shift = new Shift
        {
            StaffId = staffId,
            OpenedAt = DateTime.UtcNow,
            OpeningCash = openingCash,
            ExpectedCash = openingCash,
            Note = FormatHelper.SanitizeNote(note)
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();
        return (true, shift, null);
    }

    public async Task<(bool Success, Shift? Shift, string? ErrorMessage)> CloseShiftAsync(string staffId, decimal countedCash, string? note)
    {
        if (countedCash < 0)
            return (false, null, "Tiền thực đếm không được âm.");

        var active = await _db.Shifts.FirstOrDefaultAsync(s => s.StaffId == staffId && s.ClosedAt == null);
        if (active == null)
            return (false, null, "Không tìm thấy ca đang mở.");

        var cashCollected = await _db.Orders
            .Where(o => (o.ShiftId == active.Id || (o.PaidAt >= active.OpenedAt && o.StaffId == staffId)) &&
                        o.PaymentMethod == "CASH" &&
                        o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        active.ExpectedCash = active.OpeningCash + cashCollected;
        active.CountedCash = countedCash;
        active.Difference = countedCash - active.ExpectedCash;
        active.ClosedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            active.Note = FormatHelper.SanitizeNote(
                string.IsNullOrWhiteSpace(active.Note) ? note : $"{active.Note} | Đóng ca: {note}");
        }

        await _db.SaveChangesAsync();
        return (true, active, null);
    }
}

