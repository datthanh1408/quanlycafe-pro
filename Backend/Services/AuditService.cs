using System.Text.Json;
using CoffeeShop.Data;
using CoffeeShop.Models.Entities;

namespace CoffeeShop.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null, string? userId = null, string? userEmail = null, string? ipAddress = null);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? userId = null,
        string? userEmail = null,
        string? ipAddress = null)
    {
        var ctx = _http.HttpContext;
        var resolvedUserId = userId ?? ctx?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var resolvedEmail = userEmail ?? ctx?.User?.Identity?.Name;
        var resolvedIp = ipAddress ?? ctx?.Connection?.RemoteIpAddress?.ToString();

        var entry = new AuditLog
        {
            UserId = resolvedUserId,
            UserEmail = resolvedEmail,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = resolvedIp,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync();
    }
}

