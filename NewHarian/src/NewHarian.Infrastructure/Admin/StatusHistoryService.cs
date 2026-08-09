using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Admin;

public sealed class StatusHistoryService(
    AppDbContext db,
    IHttpContextAccessor http,
    ILogger<StatusHistoryService> logger) : IStatusHistoryService
{
    public async Task AppendOrderAsync(
        int orderId,
        string eventType,
        OrderStatus? fromStatus,
        OrderStatus? toStatus,
        bool actorIsGuest = false,
        string? guestActorName = null,
        string? messageVi = null,
        CancellationToken ct = default)
    {
        try
        {
            var (actorType, userId, actorName) = ResolveActor(actorIsGuest, guestActorName);
            db.OrderHistories.Add(new OrderHistory
            {
                OrderId = orderId,
                EventType = eventType,
                FromStatus = fromStatus.HasValue ? (int)fromStatus.Value : null,
                ToStatus = toStatus.HasValue ? (int)toStatus.Value : null,
                ActorType = actorType,
                ActorUserId = userId,
                ActorName = actorName,
                MessageVi = string.IsNullOrWhiteSpace(messageVi)
                    ? StatusHistoryMessages.ForOrder(eventType, toStatus)
                    : messageVi.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OrderHistory append failed OrderId={OrderId} Event={EventType}", orderId, eventType);
        }
    }

    public async Task AppendBookingAsync(
        int bookingId,
        string eventType,
        ServiceBookingStatus? fromStatus,
        ServiceBookingStatus? toStatus,
        bool actorIsGuest = false,
        string? guestActorName = null,
        CancellationToken ct = default)
    {
        try
        {
            var (actorType, userId, actorName) = ResolveActor(actorIsGuest, guestActorName);
            db.ServiceBookingHistories.Add(new ServiceBookingHistory
            {
                BookingId = bookingId,
                EventType = eventType,
                FromStatus = fromStatus.HasValue ? (int)fromStatus.Value : null,
                ToStatus = toStatus.HasValue ? (int)toStatus.Value : null,
                ActorType = actorType,
                ActorUserId = userId,
                ActorName = actorName,
                MessageVi = StatusHistoryMessages.ForBooking(eventType, toStatus),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BookingHistory append failed BookingId={BookingId} Event={EventType}", bookingId, eventType);
        }
    }

    public async Task<IReadOnlyList<StatusHistoryItemDto>> ListForOrderAsync(int orderId, CancellationToken ct = default)
    {
        return await db.OrderHistories.AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .Select(h => new StatusHistoryItemDto(
                h.Id, h.CreatedAt, h.EventType, h.ActorType, h.ActorName, h.MessageVi, h.FromStatus, h.ToStatus))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StatusHistoryItemDto>> ListForBookingAsync(int bookingId, CancellationToken ct = default)
    {
        return await db.ServiceBookingHistories.AsNoTracking()
            .Where(h => h.BookingId == bookingId)
            .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .Select(h => new StatusHistoryItemDto(
                h.Id, h.CreatedAt, h.EventType, h.ActorType, h.ActorName, h.MessageVi, h.FromStatus, h.ToStatus))
            .ToListAsync(ct);
    }

    private (string ActorType, string? UserId, string? ActorName) ResolveActor(bool actorIsGuest, string? guestActorName)
    {
        if (actorIsGuest)
            return ("Guest", null, string.IsNullOrWhiteSpace(guestActorName) ? "Khách" : guestActorName.Trim());

        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return ("System", null, "Hệ thống");

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = user.FindFirstValue(ClaimTypes.Email)
                   ?? user.FindFirstValue(ClaimTypes.Name)
                   ?? user.Identity?.Name
                   ?? userId;
        var actorType = user.IsInRole(AppRoles.Admin) ? "Admin"
            : user.IsInRole(AppRoles.Staff) ? "Staff"
            : "Admin";
        return (actorType, userId, name);
    }
}
