using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Address;
using NewHarian.Application.Admin;
using NewHarian.Application.Dealers;
using NewHarian.Application.Email;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

public sealed class DealerRegistrationService(
    AppDbContext db,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    IAdminNotificationService notifications,
    IVietnamDivisionCatalog catalog,
    ILogger<DealerRegistrationService> logger) : IDealerRegistrationService
{
    public const string DuplicateTaxCodeError = "MST này đã đăng ký đại lý. Vui lòng liên hệ công ty nếu cần cập nhật hồ sơ.";

    public async Task<string?> ValidateAsync(DealerFormModel model, CancellationToken ct = default)
    {
        DealerProfileValidator.Normalize(model);
        var err = DealerProfileValidator.Validate(model, catalog);
        if (err is null && await DealerProfileMapper.FindTaxCodeOwnerAsync(db, model.TaxCode, null, ct) is not null)
            err = DuplicateTaxCodeError;
        return err;
    }

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

            var err = await ValidateAsync(model, ct);
            if (err is not null)
            {
                logger.LogWarning("RegisterDealer Done rejected Error={Error}", err);
                return (false, err, null);
            }

            int? licenseId = null;
            if (model.BusinessLicenseMediaFileId is int mediaId)
            {
                if (await DealerProfileMapper.IsUsableLicenseAsync(db, mediaId, null, ct))
                    licenseId = mediaId;
                else
                    logger.LogWarning("RegisterDealer license dropped MediaFileId={MediaFileId}", mediaId);
            }

            var entity = new Dealer
            {
                Status = DealerStatus.Pending,
                BusinessLicenseMediaFileId = licenseId,
                LanguageCode = lang is "en" or "ja" ? lang : "vi",
                CreatedAt = DateTime.UtcNow
            };
            DealerProfileMapper.Apply(entity, model, catalog);
            db.Dealers.Add(entity);
            await db.SaveChangesAsync(ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.DealerCreated,
                $"Hồ sơ đại lý mới: {entity.BusinessName}",
                $"{entity.TaxCode} / {entity.Email}",
                $"/admin/Dealers?q={Uri.EscapeDataString(entity.TaxCode)}",
                "Dealer",
                entity.Id.ToString(),
                ct);

            await SendEmailsAsync(entity, ct);

            logger.LogInformation("RegisterDealer Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RegisterDealer Error");
            throw;
        }
    }

    private async Task SendEmailsAsync(Dealer entity, CancellationToken ct)
    {
        var staff = await GetSettingAsync("notifications.dealer_email", ct)
                    ?? await GetSettingAsync("company.email", ct)
                    ?? "info@harian.local";
        try
        {
            var staffMail = await emailTemplates.RenderAsync(EmailTemplateCodes.DealerStaff, new Dictionary<string, string?>
            {
                ["DealerId"] = entity.Id.ToString(),
                ["BusinessType"] = DealerProfileMapper.BusinessTypeLabel(entity.BusinessType),
                ["BusinessName"] = EmailTemplateService.Enc(entity.BusinessName),
                ["TaxCode"] = EmailTemplateService.Enc(entity.TaxCode),
                ["CustomerName"] = EmailTemplateService.Enc(entity.FullName),
                ["CustomerEmail"] = EmailTemplateService.Enc(entity.Email),
                ["CustomerPhone"] = EmailTemplateService.Enc(entity.Phone),
                ["CitizenId"] = EmailTemplateService.Enc(entity.CitizenId),
                ["Address"] = EmailTemplateService.Enc(AddressFormat.Join(entity.Address, entity.CommuneName, entity.ProvinceName)),
                ["HasBusinessLicense"] = entity.BusinessLicenseMediaFileId is null ? "Không" : "Có (xem trong Admin)",
                ["Message"] = EmailTemplateService.Enc(entity.Message)
            }, ct);
            await email.SendAsync(staff, staffMail.Subject, staffMail.Body, ct);

            var customerMail = await emailTemplates.RenderAsync(EmailTemplateCodes.DealerCustomer, new Dictionary<string, string?>
            {
                ["CustomerName"] = EmailTemplateService.Enc(entity.FullName),
                ["BusinessName"] = EmailTemplateService.Enc(entity.BusinessName)
            }, ct);
            await email.SendAsync(entity.Email, customerMail.Subject, customerMail.Body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RegisterDealer email failed Id={Id}", entity.Id);
        }
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
}
