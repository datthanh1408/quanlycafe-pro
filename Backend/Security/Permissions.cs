using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CoffeeShop.Authorization;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";
}

public static class StaffPermissions
{
    public const string ClaimType = "Permission";

    public const string ViewRevenue = "Staff.ViewRevenue";
    public const string CancelOrder = "Staff.CancelOrder";
    public const string ApplyPromotion = "Staff.ApplyPromotion";
    public const string EditCustomer = "Staff.EditCustomer";
    public const string ToggleProduct = "Staff.ToggleProduct";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [ViewRevenue] = "Xem doanh thu ca trên Dashboard nhân viên",
        [CancelOrder] = "Hủy đơn hàng (trước giai đoạn Đang pha chế)",
        [ApplyPromotion] = "Áp dụng mã khuyến mãi tại quầy POS",
        [EditCustomer] = "Cập nhật thông tin khách hàng",
        [ToggleProduct] = "Bật / tắt nhanh trạng thái 'Tạm hết' của món"
    };
}

public static class AppPolicies
{
    public const string StaffOrAdmin = "StaffOrAdmin";
    public const string CanViewRevenue = "CanViewRevenue";
    public const string CanCancelOrder = "CanCancelOrder";
    public const string CanApplyPromotion = "CanApplyPromotion";
    public const string CanEditCustomer = "CanEditCustomer";
    public const string CanToggleProduct = "CanToggleProduct";

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(StaffOrAdmin, p =>
            p.RequireRole(AppRoles.Admin, AppRoles.Staff));

        options.AddPolicy(CanViewRevenue, p =>
            p.RequireAssertion(ctx => HasAdminOrClaim(ctx.User, StaffPermissions.ViewRevenue)));

        options.AddPolicy(CanCancelOrder, p =>
            p.RequireAssertion(ctx => HasAdminOrClaim(ctx.User, StaffPermissions.CancelOrder)));

        options.AddPolicy(CanApplyPromotion, p =>
            p.RequireAssertion(ctx => HasAdminOrClaim(ctx.User, StaffPermissions.ApplyPromotion)));

        options.AddPolicy(CanEditCustomer, p =>
            p.RequireAssertion(ctx => HasAdminOrClaim(ctx.User, StaffPermissions.EditCustomer)));

        options.AddPolicy(CanToggleProduct, p =>
            p.RequireAssertion(ctx => HasAdminOrClaim(ctx.User, StaffPermissions.ToggleProduct)));
    }

    public static bool HasAdminOrClaim(ClaimsPrincipal user, string permission)
    {
        if (user.IsInRole(AppRoles.Admin)) return true;
        return user.IsInRole(AppRoles.Staff) &&
               user.HasClaim(StaffPermissions.ClaimType, permission);
    }
}

