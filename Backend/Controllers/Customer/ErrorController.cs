using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShop.Controllers;

[AllowAnonymous]
public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    [Route("/error/{code:int}")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        ViewBag.StatusCode = code;

        switch (code)
        {
            case 403:
                ViewBag.Title = "403 – Từ chối truy cập";
                ViewBag.Message = "Bạn không có quyền truy cập chức năng này.";
                ViewBag.Icon = "bi-shield-lock-fill text-warning";
                break;
            case 404:
                ViewBag.Title = "404 – Không tìm thấy trang";
                ViewBag.Message = "Trang hoặc dữ liệu bạn đang tìm kiếm không tồn tại hoặc đã được di chuyển.";
                ViewBag.Icon = "bi-compass-fill text-secondary";
                break;
            default:
                _logger.LogError("Trang lỗi hệ thống được hiển thị với mã HTTP {Code}", code);
                ViewBag.Title = "500 – Có sự cố xảy ra";
                ViewBag.Message = "Hệ thống đang gặp sự cố tạm thời khi xử lý yêu cầu. Vui lòng thử lại sau ít phút.";
                ViewBag.Icon = "bi-exclamation-triangle-fill text-danger";
                break;
        }

        return View("StatusCode");
    }
}

