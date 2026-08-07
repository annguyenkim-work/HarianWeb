using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Audit;

public sealed class AuditService(
    AppDbContext db,
    IHttpContextAccessor http,
    ILogger<AuditService> logger) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object? oldValues,
        object? newValues,
        CancellationToken ct = default)
    {
        try
        {
            var ctx = http.HttpContext;
            var userId = ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? ctx?.User?.Identity?.Name;
            var ip = ctx?.Connection.RemoteIpAddress?.ToString();

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOpts),
                NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOpts),
                IpAddress = ip,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit write failed Action={Action} Entity={EntityType}/{EntityId}",
                action, entityType, entityId);
        }
    }
}
