using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Admin;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Admin;

public sealed class AdminNotificationService(
    AppDbContext db,
    IAdminNotificationRealtime realtime,
    ILogger<AdminNotificationService> logger) : IAdminNotificationService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    public async Task PublishAsync(
        string type,
        string title,
        string? body,
        string url,
        string? entityType,
        string? entityId,
        CancellationToken ct = default)
    {
        try
        {
            var row = new AdminNotification
            {
                Type = type,
                Title = title.Length > 300 ? title[..300] : title,
                Body = body is { Length: > 500 } ? body[..500] : body,
                Url = string.IsNullOrWhiteSpace(url) ? "/admin" : url,
                EntityType = entityType,
                EntityId = entityId,
                CreatedAt = DateTime.UtcNow
            };
            db.AdminNotifications.Add(row);
            await db.SaveChangesAsync(ct);

            var dto = new AdminNotificationDto(
                row.Id, row.Type, row.Title, row.Body, row.Url, row.CreatedAt,
                row.EntityType, row.EntityId, IsRead: false);

            try
            {
                await realtime.NotifyOpsAsync(dto, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification hub push failed Id={Id}", row.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Publish notification failed Type={Type}", type);
        }
    }

    public async Task<IReadOnlyList<AdminNotificationDto>> ListAsync(string userId, int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        var since = DateTime.UtcNow - Retention;
        var rows = await db.AdminNotifications.AsNoTracking()
            .Where(n => n.CreatedAt >= since)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Body,
                n.Url,
                n.CreatedAt,
                n.EntityType,
                n.EntityId,
                IsRead = n.Reads.Any(r => r.UserId == userId)
            })
            .ToListAsync(ct);

        return rows.Select(n => new AdminNotificationDto(
            n.Id, n.Type, n.Title, n.Body, n.Url, n.CreatedAt, n.EntityType, n.EntityId, n.IsRead)).ToList();
    }

    public async Task<int> UnreadCountAsync(string userId, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - Retention;
        return await db.AdminNotifications.AsNoTracking()
            .Where(n => n.CreatedAt >= since && !n.Reads.Any(r => r.UserId == userId))
            .CountAsync(ct);
    }

    public async Task MarkReadAsync(string userId, long notificationId, CancellationToken ct = default)
    {
        var exists = await db.AdminNotificationReads
            .AnyAsync(r => r.NotificationId == notificationId && r.UserId == userId, ct);
        if (exists) return;

        var ok = await db.AdminNotifications.AnyAsync(n => n.Id == notificationId, ct);
        if (!ok) return;

        db.AdminNotificationReads.Add(new AdminNotificationRead
        {
            NotificationId = notificationId,
            UserId = userId,
            ReadAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - Retention;
        var unreadIds = await db.AdminNotifications.AsNoTracking()
            .Where(n => n.CreatedAt >= since && !n.Reads.Any(r => r.UserId == userId))
            .Select(n => n.Id)
            .ToListAsync(ct);

        foreach (var id in unreadIds)
        {
            db.AdminNotificationReads.Add(new AdminNotificationRead
            {
                NotificationId = id,
                UserId = userId,
                ReadAt = DateTime.UtcNow
            });
        }

        if (unreadIds.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
