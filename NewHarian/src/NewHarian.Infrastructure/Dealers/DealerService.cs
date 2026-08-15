using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Dealers;
using NewHarian.Application.Email;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

public sealed class DealerService(
    AppDbContext db,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    IAuditService audit,
    IAdminNotificationService notifications,
    ILogger<DealerService> logger) : IDealerService
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "email", "phone", "status"
    };

    public async Task<(bool Ok, string? Error, int? Id)> SubmitAsync(DealerFormModel model, string lang, CancellationToken ct = default)
    {
        logger.LogInformation("RegisterDealer Start");
        try
        {
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                logger.LogInformation("RegisterDealer Done honeypot");
                return (true, null, null);
            }

            var err = ValidateRegister(model);
            if (err is not null)
            {
                logger.LogWarning("RegisterDealer Done rejected Error={Error}", err);
                return (false, err, null);
            }

            var entity = new Dealer
            {
                FullName = model.FullName.Trim(),
                Phone = model.Phone.Trim(),
                Email = model.Email.Trim(),
                CitizenId = GuestValidation.NormalizeCitizenId(model.CitizenId),
                Address = model.Address.Trim(),
                Message = string.IsNullOrWhiteSpace(model.Message) ? null : model.Message.Trim(),
                Status = DealerStatus.Pending,
                LanguageCode = lang is "en" or "ja" ? lang : "vi",
                CreatedAt = DateTime.UtcNow
            };
            db.Dealers.Add(entity);
            await db.SaveChangesAsync(ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.DealerCreated,
                $"Hồ sơ đại lý mới — {entity.FullName}",
                entity.Email,
                $"/admin/Dealers?q={Uri.EscapeDataString(entity.Email)}",
                "Dealer",
                entity.Id.ToString(),
                ct);

            var staff = await GetSettingAsync("notifications.dealer_email", ct)
                        ?? await GetSettingAsync("company.email", ct)
                        ?? "info@harian.local";
            try
            {
                var staffMail = await emailTemplates.RenderAsync(EmailTemplateCodes.DealerStaff, new Dictionary<string, string?>
                {
                    ["DealerId"] = entity.Id.ToString(),
                    ["CustomerName"] = EmailTemplateService.Enc(entity.FullName),
                    ["CustomerEmail"] = EmailTemplateService.Enc(entity.Email),
                    ["CustomerPhone"] = EmailTemplateService.Enc(entity.Phone),
                    ["CitizenId"] = EmailTemplateService.Enc(entity.CitizenId),
                    ["Address"] = EmailTemplateService.Enc(entity.Address),
                    ["Message"] = EmailTemplateService.Enc(entity.Message)
                }, ct);
                await email.SendAsync(staff, staffMail.Subject, staffMail.Body, ct);

                var customerMail = await emailTemplates.RenderAsync(EmailTemplateCodes.DealerCustomer, new Dictionary<string, string?>
                {
                    ["CustomerName"] = EmailTemplateService.Enc(entity.FullName)
                }, ct);
                await email.SendAsync(entity.Email, customerMail.Subject, customerMail.Body, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RegisterDealer email failed Id={Id}", entity.Id);
            }

            logger.LogInformation("RegisterDealer Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RegisterDealer Error");
            throw;
        }
    }

    public async Task<IReadOnlyList<DealerListItemDto>> ListAsync(
        DealerStatus? status, string? q = null, string? sort = null, string? dir = null, CancellationToken ct = default)
    {
        var query = db.Dealers.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(d => d.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(d =>
                d.FullName.ToLower().Contains(term) ||
                d.Email.ToLower().Contains(term) ||
                d.Phone.ToLower().Contains(term) ||
                (d.CitizenId != null && d.CitizenId.Contains(term)));
        }

        var sortKey = AdminListQuery.NormalizeSort(sort, SortKeys, "createdAt");
        var sortDir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sortKey));
        var asc = AdminListQuery.IsAsc(sortDir);
        query = (sortKey, asc) switch
        {
            ("id", true) => query.OrderBy(d => d.Id),
            ("id", false) => query.OrderByDescending(d => d.Id),
            ("name", true) => query.OrderBy(d => d.FullName).ThenByDescending(d => d.Id),
            ("name", false) => query.OrderByDescending(d => d.FullName).ThenByDescending(d => d.Id),
            ("email", true) => query.OrderBy(d => d.Email).ThenByDescending(d => d.Id),
            ("email", false) => query.OrderByDescending(d => d.Email).ThenByDescending(d => d.Id),
            ("phone", true) => query.OrderBy(d => d.Phone).ThenByDescending(d => d.Id),
            ("phone", false) => query.OrderByDescending(d => d.Phone).ThenByDescending(d => d.Id),
            ("status", true) => query.OrderBy(d => d.Status).ThenByDescending(d => d.Id),
            ("status", false) => query.OrderByDescending(d => d.Status).ThenByDescending(d => d.Id),
            ("createdAt", true) => query.OrderBy(d => d.CreatedAt).ThenByDescending(d => d.Id),
            _ => query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id),
        };

        return await query
            .Select(d => new DealerListItemDto(d.Id, d.CreatedAt, d.FullName, d.Phone, d.Email, d.Status, d.DiscountPercent))
            .ToListAsync(ct);
    }

    public async Task<DealerDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Dealers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? null : ToDetail(d);
    }

    public async Task<(bool Ok, string? Error)> ApproveAsync(int id, decimal discountPercent, string? notes, string? userId, string? citizenId = null, CancellationToken ct = default)
    {
        logger.LogInformation("ApproveDealer Start Id={Id}", id);
        try
        {
            if (!IsPercent(discountPercent))
            {
                logger.LogWarning("ApproveDealer Done rejected Id={Id} Error={Error}", id, "percent");
                return (false, "Chiết khấu phải từ 0 đến 100.");
            }

            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null)
            {
                logger.LogWarning("ApproveDealer Done rejected Id={Id}", id);
                return (false, "Không tìm thấy.");
            }

            var from = d.Status;
            if (!TrySetCitizenId(d, citizenId, out var cccdError))
            {
                logger.LogWarning("ApproveDealer Done rejected Id={Id} Error={Error}", id, cccdError);
                return (false, cccdError);
            }
            d.Status = DealerStatus.Approved;
            d.DiscountPercent = discountPercent;
            if (notes is not null) d.InternalNotes = notes;
            d.ReviewedAt = DateTime.UtcNow;
            d.ReviewedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.Approved", "Dealer", d.Id.ToString(),
                new { Status = from.ToString() },
                new { Status = d.Status.ToString(), DiscountPercent = discountPercent },
                ct);
            logger.LogInformation("ApproveDealer Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ApproveDealer Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> RejectAsync(int id, string? notes, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("RejectDealer Start Id={Id}", id);
        try
        {
            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null)
            {
                logger.LogWarning("RejectDealer Done rejected Id={Id}", id);
                return (false, "Không tìm thấy.");
            }
            if (d.Status == DealerStatus.Approved)
            {
                logger.LogWarning("RejectDealer Done rejected Id={Id} Error={Error}", id, "already approved");
                return (false, "Không từ chối đại lý đã duyệt.");
            }

            var from = d.Status;
            d.Status = DealerStatus.Rejected;
            if (notes is not null) d.InternalNotes = notes;
            d.ReviewedAt = DateTime.UtcNow;
            d.ReviewedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.Rejected", "Dealer", d.Id.ToString(),
                new { Status = from.ToString() },
                new { Status = d.Status.ToString() },
                ct);
            logger.LogInformation("RejectDealer Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RejectDealer Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> SaveApprovedAsync(int id, decimal discountPercent, string? notes, string? userId, string? citizenId = null, CancellationToken ct = default)
    {
        logger.LogInformation("SaveDealer Start Id={Id}", id);
        try
        {
            if (!IsPercent(discountPercent))
            {
                logger.LogWarning("SaveDealer Done rejected Id={Id} Error={Error}", id, "percent");
                return (false, "Chiết khấu phải từ 0 đến 100.");
            }

            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null)
            {
                logger.LogWarning("SaveDealer Done rejected Id={Id}", id);
                return (false, "Không tìm thấy.");
            }
            if (d.Status != DealerStatus.Approved)
            {
                logger.LogWarning("SaveDealer Done rejected Id={Id} Error={Error}", id, "not approved");
                return (false, "Chỉ sửa đại lý đã duyệt.");
            }

            if (!TrySetCitizenId(d, citizenId, out var cccdError))
            {
                logger.LogWarning("SaveDealer Done rejected Id={Id} Error={Error}", id, cccdError);
                return (false, cccdError);
            }
            d.DiscountPercent = discountPercent;
            if (notes is not null) d.InternalNotes = notes;
            d.ReviewedByUserId = userId ?? d.ReviewedByUserId;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.Updated", "Dealer", d.Id.ToString(),
                null,
                new { DiscountPercent = discountPercent },
                ct);
            logger.LogInformation("SaveDealer Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveDealer Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error, int? Id)> CreateApprovedAsync(DealerCreateRequest request, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("CreateDealer Start");
        try
        {
            var err = ValidateRegister(new DealerFormModel
            {
                FullName = request.FullName,
                Phone = request.Phone,
                Email = request.Email,
                CitizenId = request.CitizenId,
                Address = request.Address,
                Message = request.Message
            });
            if (err is not null)
            {
                logger.LogWarning("CreateDealer Done rejected Error={Error}", err);
                return (false, err, null);
            }
            if (!IsPercent(request.DiscountPercent))
            {
                logger.LogWarning("CreateDealer Done rejected Error={Error}", "percent");
                return (false, "Chiết khấu phải từ 0 đến 100.", null);
            }

            var now = DateTime.UtcNow;
            var entity = new Dealer
            {
                FullName = request.FullName.Trim(),
                Phone = request.Phone.Trim(),
                Email = request.Email.Trim(),
                CitizenId = GuestValidation.NormalizeCitizenId(request.CitizenId),
                Address = request.Address.Trim(),
                Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
                Status = DealerStatus.Approved,
                DiscountPercent = request.DiscountPercent,
                InternalNotes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim(),
                ReviewedByUserId = userId,
                ReviewedAt = now,
                LanguageCode = "vi",
                CreatedAt = now
            };
            db.Dealers.Add(entity);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.Created", "Dealer", entity.Id.ToString(), null,
                new { entity.FullName, entity.Email, DiscountPercent = request.DiscountPercent }, ct);
            logger.LogInformation("CreateDealer Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateDealer Error");
            throw;
        }
    }

    public async Task<IReadOnlyList<DealerOptionDto>> ListApprovedOptionsAsync(CancellationToken ct = default)
    {
        return await db.Dealers.AsNoTracking()
            .Where(d => d.Status == DealerStatus.Approved)
            .OrderBy(d => d.FullName)
            .Select(d => new DealerOptionDto(
                d.Id, d.FullName, d.Phone, d.Email, d.Address, d.CitizenId, d.DiscountPercent ?? 0))
            .ToListAsync(ct);
    }

    private static bool IsPercent(decimal p) => p is >= 0 and <= 100;

    private static string? ValidateRegister(DealerFormModel model)
    {
        if (!GuestValidation.HasLength(model.FullName, 2, GuestValidation.NameMax))
            return "Vui lòng điền họ tên (2-200 ký tự).";
        if (string.IsNullOrWhiteSpace(model.Phone) || !GuestValidation.IsPhone(model.Phone))
            return "Số điện thoại không hợp lệ.";
        if (!GuestValidation.IsEmail(model.Email))
            return "Email không hợp lệ.";
        if (!GuestValidation.IsCitizenId(model.CitizenId))
            return "CCCD phải gồm 9 hoặc 12 chữ số.";
        if (!GuestValidation.HasLength(model.Address, 5, GuestValidation.AddressMax))
            return "Vui lòng điền địa chỉ (5-500 ký tự).";
        if (!GuestValidation.FitsMax(model.Message, GuestValidation.NotesMax))
            return "Ghi chú tối đa 2000 ký tự.";
        return null;
    }

    private static bool TrySetCitizenId(Dealer d, string? citizenId, out string? error)
    {
        var candidate = string.IsNullOrWhiteSpace(citizenId) ? d.CitizenId : citizenId;
        if (!GuestValidation.IsCitizenId(candidate))
        {
            error = "CCCD phải gồm 9 hoặc 12 chữ số.";
            return false;
        }
        d.CitizenId = GuestValidation.NormalizeCitizenId(candidate);
        error = null;
        return true;
    }

    private static DealerDetailDto ToDetail(Dealer d) => new(
        d.Id, d.CreatedAt, d.FullName, d.Phone, d.Email, d.CitizenId, d.Address, d.Message,
        d.Status, d.DiscountPercent, d.InternalNotes, d.ReviewedByUserId, d.LanguageCode, d.ReviewedAt);

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
}
