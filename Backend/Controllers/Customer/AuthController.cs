using CoffeeShop.Authorization;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoffeeShop.Controllers;

public class AuthController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                return await RedirectByRoleAsync(currentUser, returnUrl);
            }
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginLimiter")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user == null || user.IsDeleted)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa hoặc ngừng hoạt động. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm khóa 5 phút do nhập sai mật khẩu quá 5 lần. Vui lòng thử lại sau.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (user.MustChangePassword)
        {
            TempData["ToastWarning"] = "Đây là lần đăng nhập đầu tiên với mật khẩu tạm. Vui lòng đổi mật khẩu mới để tiếp tục.";
            return RedirectToAction(nameof(ChangePassword));
        }

        TempData["ToastSuccess"] = $"Xin chào {user.FullName}!";
        return await RedirectByRoleAsync(user, model.ReturnUrl);
    }

    private async Task<IActionResult> RedirectByRoleAsync(ApplicationUser user, string? returnUrl)
    {
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin))
            return Redirect("/admin/dashboard");

        if (roles.Contains(AppRoles.Staff))
            return Redirect("/staff/dashboard");

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return Redirect("/");
    }

    [HttpGet("/register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");
        return View(new RegisterViewModel());
    }

    [HttpPost("/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (existing != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được đăng ký sử dụng.");
            return View(model);
        }

        // Đăng ký công khai chỉ tạo tài khoản Customer (Mục 5.1)
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            Phone = model.Phone.Trim(),
            PhoneNumber = model.Phone.Trim(),
            Address = model.Address?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createRes = await _userManager.CreateAsync(user, model.Password);
        if (!createRes.Succeeded)
        {
            foreach (var err in createRes.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(err));
            }
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Customer);
        await _signInManager.SignInAsync(user, isPersistent: false);

        TempData["ToastSuccess"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với Đạt Mộc Coffee.";
        return Redirect("/");
    }

    [HttpPost("/logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["ToastSuccess"] = "Bạn đã đăng xuất thành công.";
        return Redirect("/");
    }

    [HttpGet("/account/change-password")]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost("/account/change-password")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Redirect("/login");

        var res = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!res.Succeeded)
        {
            foreach (var err in res.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(err));
            }
            return View(model);
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["ToastSuccess"] = "Đổi mật khẩu thành công!";
        return await RedirectByRoleAsync(user, null);
    }

    [HttpGet("/forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost("/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập địa chỉ email đã đăng ký.");
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user != null && user.IsActive && !user.IsDeleted)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action(nameof(ResetPassword), "Auth", new { email = user.Email, token }, Request.Scheme);
            // Mục 5.1: môi trường Dev ghi liên kết đặt lại ra console/log thay vì gửi mail thật
            _logger.LogInformation("=== [DEV EMAIL SENDER] Liên kết đặt lại mật khẩu cho {Email}: {ResetLink} ===", user.Email, link);
        }

        ViewBag.Sent = true;
        return View();
    }

    [HttpGet("/reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email, string token)
    {
        ViewBag.Email = email;
        ViewBag.Token = token;
        return View();
    }

    [HttpPost("/reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
    {
        ViewBag.Email = email;
        ViewBag.Token = token;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            TempData["ToastSuccess"] = "Nếu tài khoản tồn tại, mật khẩu đã được đặt lại.";
            return Redirect("/login");
        }

        var res = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!res.Succeeded)
        {
            foreach (var err in res.Errors)
                ModelState.AddModelError(string.Empty, TranslateIdentityError(err));
            return View();
        }

        TempData["ToastSuccess"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập.";
        return Redirect("/login");
    }

    private static string TranslateIdentityError(IdentityError err) => err.Code switch
    {
        "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
        "PasswordRequiresUpper" => "Mật khẩu phải chứa ít nhất 1 chữ cái in hoa (A-Z).",
        "PasswordRequiresLower" => "Mật khẩu phải chứa ít nhất 1 chữ cái viết thường (a-z).",
        "PasswordRequiresDigit" => "Mật khẩu phải chứa ít nhất 1 chữ số (0-9).",
        "PasswordMismatch" => "Mật khẩu hiện tại không chính xác.",
        "DuplicateEmail" or "DuplicateUserName" => "Email này đã tồn tại trong hệ thống.",
        _ => err.Description
    };
}

