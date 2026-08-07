using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Catalog;
using NewHarian.Application.Email;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class ServiceBookingService(
    AppDbContext db,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    IAuditService audit,
    IStatusHistoryService history,
    IAdminNotificationService notifications,
    ILogger<ServiceBookingService> logger) : IServiceBookingService
{
    public async Task<(bool Ok, string? Error, string? BookingNumber)> CreateAsync(ServiceBookingRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("CreateBooking Start VariantId={VariantId}", request.ServiceVariantId);
        try
        {
            var variant = await db.ServiceVariants
                .Include(v => v.Service).ThenInclude(s => s.Translations)
                .FirstOrDefaultAsync(v => v.Id == request.ServiceVariantId && v.IsActive, ct);

            if (variant is null || variant.Service.Status != ProductStatus.Published)
                return RejectCreate("Dịch vụ không hợp lệ.");

            if (!GuestValidation.HasLength(request.CustomerName, 2, GuestValidation.NameMax))
                return RejectCreate("Vui lòng điền họ tên (2-200 ký tự)."); // BOOK_REQUIRED
            if (!GuestValidation.IsEmail(request.CustomerEmail))
                return RejectCreate("Email không hợp lệ."); // BOOK_EMAIL_INVALID
            if (string.IsNullOrWhiteSpace(request.CustomerPhone) || !GuestValidation.IsPhone(request.CustomerPhone))
                return RejectCreate("Số điện thoại không hợp lệ (8-20 ký tự)."); // BOOK_PHONE_INVALID

            if (request.PreferredDate < DateOnly.FromDateTime(DateTime.UtcNow.Date))
                return RejectCreate("Ngày hẹn phải từ hôm nay trở đi.");

            var time = request.PreferredTime?.Trim() ?? "";
            if (time is not ("Sáng" or "Chiều"))
                return RejectCreate("Vui lòng chọn khung giờ Sáng hoặc Chiều.");

            if (!GuestValidation.FitsMax(request.ServiceAddress, GuestValidation.AddressMax))
                return RejectCreate("Địa chỉ tối đa 500 ký tự.");
            if (!GuestValidation.FitsMax(request.Notes, GuestValidation.NotesMax))
                return RejectCreate("Ghi chú tối đa 2000 ký tự.");

            var isAtHome = variant.VariantLabel.Contains("nhà", StringComparison.OrdinalIgnoreCase)
                           || variant.VariantLabel.Contains("home", StringComparison.OrdinalIgnoreCase)
                           || variant.Sku.Contains("HOME", StringComparison.OrdinalIgnoreCase);
            if (isAtHome && string.IsNullOrWhiteSpace(request.ServiceAddress))
                return RejectCreate("Vui lòng nhập địa chỉ khi chọn dịch vụ tại nhà.");

            var bookingNumber = await NextBookingNumberAsync(ct);
            var booking = new ServiceBooking
            {
                BookingNumber = bookingNumber,
                ServiceId = variant.ServiceId,
                ServiceVariantId = variant.Id,
                CustomerName = request.CustomerName.Trim(),
                CustomerEmail = request.CustomerEmail.Trim(),
                CustomerPhone = request.CustomerPhone.Trim(),
                PreferredDate = request.PreferredDate,
                PreferredTime = time,
                ServiceAddress = request.ServiceAddress?.Trim(),
                Notes = request.Notes?.Trim(),
                Status = ServiceBookingStatus.New,
                LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "vi" : request.LanguageCode,
                CreatedAt = DateTime.UtcNow
            };

            db.ServiceBookings.Add(booking);
            await db.SaveChangesAsync(ct);

            await history.AppendBookingAsync(
                booking.Id,
                StatusHistoryEventTypes.Created,
                null,
                ServiceBookingStatus.New,
                actorIsGuest: true,
                guestActorName: booking.CustomerEmail,
                ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.ServiceBookingCreated,
                $"Đặt lịch mới {booking.BookingNumber}",
                $"{booking.CustomerName} · {booking.PreferredDate:yyyy-MM-dd}",
                $"/admin/ServiceBookings?q={Uri.EscapeDataString(booking.BookingNumber)}",
                "ServiceBooking",
                booking.Id.ToString(),
                ct);

            var serviceName = variant.Service.Translations.FirstOrDefault(t => t.LanguageCode == booking.LanguageCode)?.Name
                              ?? variant.Service.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                              ?? variant.Service.Slug;

            var staffTo = await GetSettingAsync("notifications.service_booking_email")
                          ?? await GetSettingAsync("company.email")
                          ?? "info@harian.local";

            var vars = new Dictionary<string, string?>
            {
                ["BookingId"] = EmailTemplateService.Enc(booking.BookingNumber),
                ["BookingNumber"] = EmailTemplateService.Enc(booking.BookingNumber),
                ["ProductName"] = EmailTemplateService.Enc(serviceName),
                ["VariantLabel"] = EmailTemplateService.Enc(variant.VariantLabel),
                ["CustomerName"] = EmailTemplateService.Enc(booking.CustomerName),
                ["CustomerEmail"] = EmailTemplateService.Enc(booking.CustomerEmail),
                ["CustomerPhone"] = EmailTemplateService.Enc(booking.CustomerPhone),
                ["PreferredDate"] = booking.PreferredDate.ToString("yyyy-MM-dd"),
                ["PreferredTime"] = EmailTemplateService.Enc(booking.PreferredTime),
                ["ServiceAddress"] = EmailTemplateService.Enc(booking.ServiceAddress),
                ["Notes"] = EmailTemplateService.Enc(booking.Notes)
            };

            try
            {
                var staffMail = await emailTemplates.RenderAsync(EmailTemplateCodes.BookingStaff, vars, ct);
                await email.SendAsync(staffTo, staffMail.Subject, staffMail.Body, ct);
                var customerMail = await emailTemplates.RenderAsync(EmailTemplateCodes.BookingCustomer, vars, ct);
                await email.SendAsync(booking.CustomerEmail, customerMail.Subject, customerMail.Body, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed sending service booking emails for {BookingNumber}", booking.BookingNumber);
            }

            logger.LogInformation("CreateBooking Done BookingNumber={BookingNumber}", booking.BookingNumber);
            return (true, null, booking.BookingNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateBooking Error VariantId={VariantId}", request.ServiceVariantId);
            throw;
        }
    }

    private (bool Ok, string? Error, string? BookingNumber) RejectCreate(string error)
    {
        logger.LogWarning("CreateBooking Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private async Task<string> NextBookingNumberAsync(CancellationToken ct)
    {
        var prefix = PublicReferenceCodes.ServicePrefix;
        var existing = await db.ServiceBookings.AsNoTracking()
            .Where(b => b.BookingNumber.StartsWith(prefix))
            .Select(b => b.BookingNumber)
            .ToListAsync(ct);
        return PublicReferenceCodes.Format(prefix, PublicReferenceCodes.NextSequence(existing, prefix));
    }

    public async Task<IReadOnlyList<ServiceBookingListItemDto>> ListAsync(
        ServiceBookingStatus? status,
        string? q = null,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var query = db.ServiceBookings.AsNoTracking()
            .Include(b => b.Service).ThenInclude(s => s.Translations)
            .Include(b => b.ServiceVariant)
            .AsQueryable();
        if (status.HasValue) query = query.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b =>
                b.BookingNumber.ToLower().Contains(term) ||
                b.CustomerName.ToLower().Contains(term) ||
                b.CustomerEmail.ToLower().Contains(term) ||
                b.CustomerPhone.ToLower().Contains(term));
        }

        (from, to) = AdminListQuery.NormalizeDateRange(from, to);
        if (from is DateOnly fromDate) query = query.Where(b => b.PreferredDate >= fromDate);
        if (to is DateOnly toDate) query = query.Where(b => b.PreferredDate <= toDate);

        var sortKey = AdminListQuery.NormalizeSort(sort, BookingSortKeys, "createdAt");
        var sortDir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sortKey));
        var asc = AdminListQuery.IsAsc(sortDir);

        query = (sortKey, asc) switch
        {
            ("bookingNumber", true) => query.OrderBy(b => b.BookingNumber).ThenByDescending(b => b.Id),
            ("bookingNumber", false) => query.OrderByDescending(b => b.BookingNumber).ThenByDescending(b => b.Id),
            ("id", true) => query.OrderBy(b => b.Id),
            ("id", false) => query.OrderByDescending(b => b.Id),
            ("customer", true) => query.OrderBy(b => b.CustomerName).ThenByDescending(b => b.Id),
            ("customer", false) => query.OrderByDescending(b => b.CustomerName).ThenByDescending(b => b.Id),
            ("phone", true) => query.OrderBy(b => b.CustomerPhone).ThenByDescending(b => b.Id),
            ("phone", false) => query.OrderByDescending(b => b.CustomerPhone).ThenByDescending(b => b.Id),
            ("product", true) => query
                .OrderBy(b => b.Service.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Name).FirstOrDefault() ?? b.Service.Slug)
                .ThenByDescending(b => b.Id),
            ("product", false) => query
                .OrderByDescending(b => b.Service.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Name).FirstOrDefault() ?? b.Service.Slug)
                .ThenByDescending(b => b.Id),
            ("preferredDate", true) => query.OrderBy(b => b.PreferredDate).ThenBy(b => b.PreferredTime).ThenByDescending(b => b.Id),
            ("preferredDate", false) => query.OrderByDescending(b => b.PreferredDate).ThenByDescending(b => b.PreferredTime).ThenByDescending(b => b.Id),
            ("status", true) => query.OrderBy(b => b.Status).ThenByDescending(b => b.Id),
            ("status", false) => query.OrderByDescending(b => b.Status).ThenByDescending(b => b.Id),
            ("createdAt", true) => query.OrderBy(b => b.CreatedAt).ThenByDescending(b => b.Id),
            _ => query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id),
        };

        var list = await query.ToListAsync(ct);
        return list.Select(b => new ServiceBookingListItemDto(
            b.Id,
            b.BookingNumber,
            b.CustomerName,
            b.CustomerEmail,
            b.CustomerPhone,
            b.Service.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? b.Service.Slug,
            b.ServiceVariant.VariantLabel,
            b.PreferredDate,
            b.PreferredTime,
            b.Status,
            b.CreatedAt)).ToList();
    }

    private static readonly HashSet<string> BookingSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "bookingNumber", "id", "customer", "phone", "product", "preferredDate", "status", "createdAt"
    };

    public async Task<ServiceBookingDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var b = await db.ServiceBookings.AsNoTracking()
            .Include(x => x.Service).ThenInclude(s => s.Translations)
            .Include(x => x.ServiceVariant)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return null;
        return new ServiceBookingDetailDto(
            b.Id, b.BookingNumber, b.CustomerName, b.CustomerEmail, b.CustomerPhone,
            b.Service.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? b.Service.Slug,
            b.ServiceVariant.VariantLabel,
            b.PreferredDate, b.PreferredTime, b.ServiceAddress, b.Notes, b.InternalNotes,
            b.Status, b.LanguageCode, b.CreatedAt);
    }

    public async Task<bool> UpdateStatusAsync(int id, ServiceBookingStatus status, string? internalNotes, CancellationToken ct = default)
    {
        logger.LogInformation("UpdateBookingStatus Start Id={Id} Status={Status}", id, status);
        try
        {
            var b = await db.ServiceBookings.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (b is null)
            {
                logger.LogWarning("UpdateBookingStatus Done rejected Id={Id}", id);
                return false;
            }
            var from = b.Status;
            b.Status = status;
            if (internalNotes is not null) b.InternalNotes = internalNotes;
            if (status == ServiceBookingStatus.Confirmed) b.ConfirmedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "ServiceBooking.StatusChanged",
                "ServiceBooking",
                b.Id.ToString(),
                new { Status = from.ToString() },
                new { Status = status.ToString(), InternalNotes = internalNotes },
                ct);
            await history.AppendBookingAsync(
                b.Id,
                StatusHistoryEventTypes.StatusChanged,
                from,
                status,
                ct: ct);
            logger.LogInformation("UpdateBookingStatus Done Id={Id} Status={Status}", id, status);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateBookingStatus Error Id={Id}", id);
            throw;
        }
    }

    private async Task<string?> GetSettingAsync(string key)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
}
