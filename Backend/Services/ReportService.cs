using ClosedXML.Excel;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services;

public class DashboardSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedPaidOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalProducts { get; set; }
    public List<Ingredient> LowStockIngredients { get; set; } = new();
    public List<(string Label, decimal Revenue, int OrderCount)> RevenueSeries { get; set; } = new();
    public List<(string ProductName, int QuantitySold, decimal Revenue)> TopProducts { get; set; } = new();
    public List<(string ProductName, int QuantitySold, decimal Revenue)> SlowProducts { get; set; } = new();
    public List<(string StatusLabel, int Count, string Color)> OrdersByStatus { get; set; } = new();
    public List<Order> RecentOrders { get; set; } = new();
    public List<(string StaffName, int HandledCount, decimal Revenue)> StaffPerformance { get; set; } = new();
    public List<(string CustomerName, string Email, int OrderCount, decimal TotalSpent)> TopCustomers { get; set; } = new();
}

public interface IReportService
{
    Task<DashboardSummaryDto> GetAdminSummaryAsync(DateTime fromUtc, DateTime toUtc);
    Task<byte[]> ExportOrdersExcelAsync(DateTime? fromUtc, DateTime? toUtc, OrderStatus? status);
    Task<byte[]> ExportComprehensiveReportExcelAsync(DateTime fromUtc, DateTime toUtc);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<DashboardSummaryDto> GetAdminSummaryAsync(DateTime fromUtc, DateTime toUtc)
    {
        var ordersInRange = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Staff)
            .Include(o => o.Customer)
            .Include(o => o.Table)
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        // Quy tắc Mục 7.3: Doanh thu chỉ tính đơn "Hoàn thành" và "Đã thanh toán"
        var revenueOrders = ordersInRange
            .Where(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid)
            .ToList();

        var totalRevenue = revenueOrders.Sum(o => o.Total);

        var processingCount = await _db.Orders.CountAsync(o =>
            o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);

        var customerUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Customer);
        var activeCustomerCount = customerUsers.Count(u => !u.IsDeleted);

        var productCount = await _db.Products.CountAsync(p => !p.IsDeleted);

        var lowStocks = await _db.Ingredients
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.IsActive && i.StockQty <= i.MinThreshold)
            .OrderBy(i => i.StockQty)
            .ToListAsync();

        // Chuỗi doanh thu theo ngày (theo múi giờ VN)
        var startVn = FormatHelper.ToVnTime(fromUtc).Date;
        var endVn = FormatHelper.ToVnTime(toUtc).Date;
        var daysSpan = Math.Clamp((int)(endVn - startVn).TotalDays, 0, 365);

        var series = new List<(string Label, decimal Revenue, int OrderCount)>();
        var groupedByDay = revenueOrders
            .GroupBy(o => FormatHelper.ToVnTime(o.CreatedAt).Date)
            .ToDictionary(g => g.Key, g => (Rev: g.Sum(x => x.Total), Cnt: g.Count()));

        for (int d = 0; d <= daysSpan; d++)
        {
            var date = startVn.AddDays(d);
            if (groupedByDay.TryGetValue(date, out var val))
                series.Add((date.ToString("dd/MM"), val.Rev, val.Cnt));
            else
                series.Add((date.ToString("dd/MM"), 0m, 0));
        }

        // Top sản phẩm bán chạy & bán chậm
        var allProducts = await _db.Products.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync();
        var itemStats = revenueOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => (Name: g.Key.ProductName, Id: g.Key.ProductId, Qty: g.Sum(x => x.Quantity), Rev: g.Sum(x => x.LineTotal)))
            .ToList();

        var topProducts = itemStats
            .OrderByDescending(x => x.Qty)
            .ThenByDescending(x => x.Rev)
            .Take(7)
            .Select(x => (x.Name, x.Qty, x.Rev))
            .ToList();

        var soldMap = itemStats.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => (Qty: g.Sum(v => v.Qty), Rev: g.Sum(v => v.Rev)));
        var slowProducts = allProducts
            .Select(p =>
            {
                var st = soldMap.TryGetValue(p.Id, out var s) ? s : (0, 0m);
                return (p.Name, st.Item1, st.Item2);
            })
            .OrderBy(x => x.Item2)
            .ThenBy(x => x.Item3)
            .Take(7)
            .ToList();

        // Đơn theo trạng thái
        var byStatus = Enum.GetValues<OrderStatus>()
            .Select(st => (
                StatusLabel: st.GetDisplayName(),
                Count: ordersInRange.Count(o => o.Status == st),
                Color: st.GetBadgeColor()))
            .Where(x => x.Count > 0)
            .ToList();

        // Hiệu suất nhân viên
        var staffPerf = revenueOrders
            .Where(o => o.Staff != null)
            .GroupBy(o => o.Staff!.FullName)
            .Select(g => (StaffName: g.Key, HandledCount: g.Count(), Revenue: g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // Top khách hàng chi tiêu cao nhất
        var topCustomers = revenueOrders
            .Where(o => o.Customer != null)
            .GroupBy(o => new { o.Customer!.FullName, Email = o.Customer.Email ?? "" })
            .Select(g => (g.Key.FullName, g.Key.Email, OrderCount: g.Count(), TotalSpent: g.Sum(x => x.Total)))
            .OrderByDescending(x => x.TotalSpent)
            .Take(10)
            .ToList();

        return new DashboardSummaryDto
        {
            TotalRevenue = totalRevenue,
            TotalOrders = ordersInRange.Count,
            CompletedPaidOrders = revenueOrders.Count,
            ProcessingOrders = processingCount,
            TotalCustomers = activeCustomerCount,
            TotalProducts = productCount,
            LowStockIngredients = lowStocks,
            RevenueSeries = series,
            TopProducts = topProducts,
            SlowProducts = slowProducts,
            OrdersByStatus = byStatus,
            RecentOrders = ordersInRange.Take(10).ToList(),
            StaffPerformance = staffPerf,
            TopCustomers = topCustomers
        };
    }

    public async Task<byte[]> ExportOrdersExcelAsync(DateTime? fromUtc, DateTime? toUtc, OrderStatus? status)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Staff)
            .Include(o => o.Items)
            .AsQueryable();

        if (fromUtc.HasValue) query = query.Where(o => o.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(o => o.CreatedAt <= toUtc.Value);
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DanhSachDonHang");

        string[] headers =
        {
            "Mã đơn", "Thời gian đặt (VN)", "Khách hàng", "SĐT", "Hình thức", "Bàn",
            "Nguồn", "Số món", "Tạm tính (₫)", "Giảm giá (₫)", "Phí giao (₫)", "Tổng tiền (₫)",
            "Thanh toán", "TT Thanh toán", "Trạng thái đơn", "Nhân viên xử lý"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#6F4E37");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var o in orders)
        {
            ws.Cell(row, 1).Value = o.Code;
            ws.Cell(row, 2).Value = FormatHelper.DateTimeVn(o.CreatedAt);
            ws.Cell(row, 3).Value = o.CustomerName;
            ws.Cell(row, 4).Value = o.Phone ?? "";
            ws.Cell(row, 5).Value = o.OrderType.GetDisplayName();
            ws.Cell(row, 6).Value = o.Table?.Code ?? "-";
            ws.Cell(row, 7).Value = o.Source.GetDisplayName();
            ws.Cell(row, 8).Value = o.Items.Sum(i => i.Quantity);
            ws.Cell(row, 9).Value = o.Subtotal;
            ws.Cell(row, 10).Value = o.DiscountAmount;
            ws.Cell(row, 11).Value = o.ShippingFee;
            ws.Cell(row, 12).Value = o.Total;
            ws.Cell(row, 13).Value = o.PaymentMethod;
            ws.Cell(row, 14).Value = o.PaymentStatus.GetDisplayName();
            ws.Cell(row, 15).Value = o.Status.GetDisplayName();
            ws.Cell(row, 16).Value = o.Staff?.FullName ?? "-";

            for (int m = 9; m <= 12; m++)
                ws.Cell(row, m).Style.NumberFormat.Format = "#,##0";

            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportComprehensiveReportExcelAsync(DateTime fromUtc, DateTime toUtc)
    {
        var summary = await GetAdminSummaryAsync(fromUtc, toUtc);
        var stockTx = await _db.StockTransactions
            .AsNoTracking()
            .Include(t => t.Ingredient)
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt <= toUtc)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        using var wb = new XLWorkbook();

        // Sheet 1: Tổng quan & Doanh thu ngày
        var ws1 = wb.Worksheets.Add("DoanhThuTheoNgay");
        ws1.Cell(1, 1).Value = "BÁO CÁO KINH DOANH ĐẠT MỘC COFFEE";
        ws1.Cell(1, 1).Style.Font.Bold = true;
        ws1.Cell(1, 1).Style.Font.FontSize = 14;
        ws1.Cell(2, 1).Value = $"Kỳ báo cáo: {FormatHelper.DateTimeVn(fromUtc, "dd/MM/yyyy")} - {FormatHelper.DateTimeVn(toUtc, "dd/MM/yyyy")}";
        ws1.Cell(3, 1).Value = "Tổng doanh thu (Đơn Hoàn thành & Đã thanh toán):";
        ws1.Cell(3, 2).Value = summary.TotalRevenue;
        ws1.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws1.Cell(4, 1).Value = "Tổng số đơn trong kỳ:";
        ws1.Cell(4, 2).Value = summary.TotalOrders;

        ws1.Cell(6, 1).Value = "Ngày";
        ws1.Cell(6, 2).Value = "Số đơn hoàn thành";
        ws1.Cell(6, 3).Value = "Doanh thu (₫)";
        ws1.Range(6, 1, 6, 3).Style.Font.Bold = true;
        ws1.Range(6, 1, 6, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#6F4E37");
        ws1.Range(6, 1, 6, 3).Style.Font.FontColor = XLColor.White;

        int r = 7;
        foreach (var s in summary.RevenueSeries)
        {
            ws1.Cell(r, 1).Value = s.Label;
            ws1.Cell(r, 2).Value = s.OrderCount;
            ws1.Cell(r, 3).Value = s.Revenue;
            ws1.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            r++;
        }
        ws1.Columns().AdjustToContents();

        // Sheet 2: Sản phẩm bán chạy
        var ws2 = wb.Worksheets.Add("SanPhamBanChay");
        ws2.Cell(1, 1).Value = "Tên sản phẩm";
        ws2.Cell(1, 2).Value = "Số lượng bán";
        ws2.Cell(1, 3).Value = "Doanh thu dòng (₫)";
        ws2.Range(1, 1, 1, 3).Style.Font.Bold = true;
        r = 2;
        foreach (var p in summary.TopProducts)
        {
            ws2.Cell(r, 1).Value = p.ProductName;
            ws2.Cell(r, 2).Value = p.QuantitySold;
            ws2.Cell(r, 3).Value = p.Revenue;
            ws2.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            r++;
        }
        ws2.Columns().AdjustToContents();

        // Sheet 3: Hiệu suất nhân viên
        var ws3 = wb.Worksheets.Add("HieuSuatNhanVien");
        ws3.Cell(1, 1).Value = "Nhân viên";
        ws3.Cell(1, 2).Value = "Số đơn hoàn tất";
        ws3.Cell(1, 3).Value = "Doanh thu phụ trách (₫)";
        ws3.Range(1, 1, 1, 3).Style.Font.Bold = true;
        r = 2;
        foreach (var st in summary.StaffPerformance)
        {
            ws3.Cell(r, 1).Value = st.StaffName;
            ws3.Cell(r, 2).Value = st.HandledCount;
            ws3.Cell(r, 3).Value = st.Revenue;
            ws3.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            r++;
        }
        ws3.Columns().AdjustToContents();

        // Sheet 4: Biến động nhập xuất kho
        var ws4 = wb.Worksheets.Add("NhapXuatKho");
        ws4.Cell(1, 1).Value = "Thời gian (VN)";
        ws4.Cell(1, 2).Value = "Mã NL";
        ws4.Cell(1, 3).Value = "Tên nguyên liệu";
        ws4.Cell(1, 4).Value = "Loại giao dịch";
        ws4.Cell(1, 5).Value = "Biến động";
        ws4.Cell(1, 6).Value = "Tồn sau GD";
        ws4.Cell(1, 7).Value = "Đơn vị";
        ws4.Cell(1, 8).Value = "Lý do";
        ws4.Cell(1, 9).Value = "Người thực hiện";
        ws4.Range(1, 1, 1, 9).Style.Font.Bold = true;
        r = 2;
        foreach (var t in stockTx)
        {
            ws4.Cell(r, 1).Value = FormatHelper.DateTimeVn(t.CreatedAt);
            ws4.Cell(r, 2).Value = t.Ingredient.Code;
            ws4.Cell(r, 3).Value = t.Ingredient.Name;
            ws4.Cell(r, 4).Value = t.Type.GetDisplayName();
            ws4.Cell(r, 5).Value = t.Quantity;
            ws4.Cell(r, 6).Value = t.BalanceAfter;
            ws4.Cell(r, 7).Value = t.Ingredient.Unit;
            ws4.Cell(r, 8).Value = t.Reason;
            ws4.Cell(r, 9).Value = t.CreatedBy;
            r++;
        }
        ws4.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}

