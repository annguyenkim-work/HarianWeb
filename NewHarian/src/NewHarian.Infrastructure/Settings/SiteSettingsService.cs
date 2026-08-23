using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Application.Payments;
using NewHarian.Application.Settings;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Settings;

public sealed class SiteSettingsService(
    AppDbContext db,
    IMediaStorage media,
    IEmailSender email,
    IConfiguration config,
    IVietQrService vietQr,
    ISiteChromeCache chrome,
    ILogger<SiteSettingsService> logger) : ISiteSettingsService
{
    public async Task<BankSettingsDto> GetBankAsync(CancellationToken ct = default)
    {
        var map = await LoadMapAsync("company", ct);
        var model = new BankSettingsDto
        {
            BankBin = map.GetValueOrDefault("company.bank.bin") ?? "",
            BankName = map.GetValueOrDefault("company.bank.name") ?? "",
            BankAccount = map.GetValueOrDefault("company.bank.account") ?? "",
            AccountHolderName = map.GetValueOrDefault("company.bank.account_name") ?? "",
            BankBranch = map.GetValueOrDefault("company.bank.branch") ?? ""
        };
        if (!string.IsNullOrWhiteSpace(model.BankBin) && !string.IsNullOrWhiteSpace(model.BankAccount))
            model.VietQrPreviewDataUrl = vietQr.CreatePngDataUrl(
                model.BankBin, model.BankAccount, 10000, "HAR-ORDER-TEST", model.AccountHolderName);
        return model;
    }

    public async Task SaveBankAsync(BankSettingsDto model, CancellationToken ct = default)
    {
        logger.LogInformation("SaveBankSettings Start");
        try
        {
            var bin = (model.BankBin ?? "").Trim();
            if (VnBankCatalog.FindByBin(bin) is { } bank)
                model.BankName = bank.DisplayName;

            await UpsertManyAsync("company", new Dictionary<string, string>
            {
                ["company.bank.bin"] = bin,
                ["company.bank.name"] = (model.BankName ?? "").Trim(),
                ["company.bank.account"] = (model.BankAccount ?? "").Trim(),
                ["company.bank.account_name"] = (model.AccountHolderName ?? "").Trim(),
                ["company.bank.branch"] = model.BankBranch ?? ""
            }, ["company.bank.qr"], ct);
            logger.LogInformation("SaveBankSettings Done");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveBankSettings Error");
            throw;
        }
    }

    public async Task<BrandSettingsDto> GetBrandAsync(CancellationToken ct = default)
    {
        var map = await LoadMapAsync("company", ct);
        return new BrandSettingsDto
        {
            BrandName = map.GetValueOrDefault("company.brand") ?? "Harian",
            CompanyName = map.GetValueOrDefault("company.name") ?? "",
            LogoUrl = map.GetValueOrDefault("company.logo") ?? "",
            Phone = map.GetValueOrDefault("company.phone") ?? "",
            Phone2 = map.GetValueOrDefault("company.phone2") ?? "",
            Email = map.GetValueOrDefault("company.email") ?? "",
            Address = map.GetValueOrDefault("company.address") ?? "",
            TaglineVi = map.GetValueOrDefault("company.tagline.vi") ?? "",
            TaglineEn = map.GetValueOrDefault("company.tagline.en") ?? "",
            TaglineJa = map.GetValueOrDefault("company.tagline.ja") ?? "",
            FacebookUrl = map.GetValueOrDefault("company.facebook") ?? "",
            InstagramUrl = map.GetValueOrDefault("company.instagram") ?? ""
        };
    }

    public async Task SaveBrandAsync(
        BrandSettingsDto model,
        SettingsFileUpload? logo,
        CancellationToken ct = default)
    {
        logger.LogInformation("SaveBrandSettings Start");
        try
        {
            var logoUrl = model.LogoUrl?.Trim() ?? "";
            if (logo is not null)
            {
                var uploaded = await media.SaveImageAsync(
                    logo.Content, logo.FileName, logo.ContentType, logo.UploadedByUserId, ct, "brand");
                logoUrl = uploaded.Url;
                model.LogoUrl = logoUrl;
            }

            await UpsertManyAsync("company", new Dictionary<string, string>
            {
                ["company.brand"] = (model.BrandName ?? "").Trim(),
                ["company.name"] = (model.CompanyName ?? "").Trim(),
                ["company.phone"] = (model.Phone ?? "").Trim(),
                ["company.phone2"] = (model.Phone2 ?? "").Trim(),
                ["company.email"] = (model.Email ?? "").Trim(),
                ["company.address"] = (model.Address ?? "").Trim(),
                ["company.tagline.vi"] = (model.TaglineVi ?? "").Trim(),
                ["company.tagline.en"] = (model.TaglineEn ?? "").Trim(),
                ["company.tagline.ja"] = (model.TaglineJa ?? "").Trim(),
                ["company.facebook"] = (model.FacebookUrl ?? "").Trim(),
                ["company.instagram"] = (model.InstagramUrl ?? "").Trim(),
                ["company.logo"] = logoUrl
            }, null, ct);
            logger.LogInformation("SaveBrandSettings Done");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveBrandSettings Error");
            throw;
        }
    }

    public async Task ClearLogoAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ClearLogo Start");
        try
        {
            await UpsertManyAsync("company", new Dictionary<string, string> { ["company.logo"] = "" }, null, ct);
            logger.LogInformation("ClearLogo Done");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ClearLogo Error");
            throw;
        }
    }

    public async Task<EmailSettingsDto> GetEmailAsync(CancellationToken ct = default)
    {
        var map = await LoadMapAsync("notifications", ct);
        var company = await LoadMapAsync("company", ct);
        return BuildEmailModel(map, company);
    }

    public async Task<SettingsSaveResult> SaveEmailAsync(EmailSettingsDto model, CancellationToken ct = default)
    {
        logger.LogInformation("SaveEmailSettings Start");
        try
        {
            var errors = ValidateEmails(model);
            if (errors.Count > 0)
            {
                logger.LogWarning("SaveEmailSettings Done rejected");
                var current = await GetEmailAsync(ct);
                model.CompanyEmail = current.CompanyEmail;
                FillSmtpStatus(model);
                return new SettingsSaveResult(false, errors);
            }

            await UpsertManyAsync("notifications", new Dictionary<string, string>
            {
                ["notifications.order_email"] = model.OrderEmail.Trim(),
                ["notifications.inquiry_email"] = model.InquiryEmail.Trim(),
                ["notifications.application_email"] = model.ApplicationEmail.Trim(),
                ["notifications.service_booking_email"] = model.ServiceBookingEmail.Trim(),
                ["notifications.dealer_email"] = model.DealerEmail.Trim()
            }, null, ct);
            logger.LogInformation("SaveEmailSettings Done");
            return new SettingsSaveResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveEmailSettings Error");
            throw;
        }
    }

    public async Task<TestEmailResult> TestEmailAsync(string? testTo, CancellationToken ct = default)
    {
        logger.LogInformation("TestEmail Start To={To}", testTo);
        try
        {
            var to = (testTo ?? "").Trim();
            if (!GuestValidation.IsEmail(to))
            {
                logger.LogWarning("TestEmail Done rejected Error={Error}", "invalid to");
                return new TestEmailResult(false, "Email nhận thử không hợp lệ.");
            }

            var smtpOn = config.GetValue("Email:Smtp:Enabled", false);
            var body =
                "<p>Đây là email thử từ Admin - Harian.</p>" +
                $"<p>SMTP Enabled = <b>{(smtpOn ? "true" : "false")}</b></p>" +
                $"<p>Thời gian (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>" +
                (smtpOn
                    ? "<p>Nếu bạn nhận được thư này trong hộp thư, SMTP đã hoạt động.</p>"
                    : "<p>SMTP đang tắt - thư chỉ được ghi vào <code>App_Data/outbox</code> trên server.</p>");
            await email.SendAsync(to, "[Harian] Email thử nghiệm", body, ct);
            logger.LogInformation("TestEmail Done To={To} SmtpEnabled={Smtp}", to, smtpOn);
            return new TestEmailResult(true, smtpOn
                ? $"Đã gửi thử tới {to}. Kiểm tra hộp thư (và thư mục spam)."
                : $"SMTP đang tắt - đã ghi file outbox cho {to}. Bật Email:Smtp:Enabled trong appsettings để gửi thật.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestEmail Error To={To}", testTo);
            return new TestEmailResult(false, "Gửi thử thất bại: " + ex.Message);
        }
    }

    public async Task<InventorySettingsDto> GetInventoryAsync(CancellationToken ct = default)
    {
        var map = await LoadMapAsync("inventory", ct);
        var lowRaw = map.GetValueOrDefault("inventory.low_stock_threshold");
        var expRaw = map.GetValueOrDefault("inventory.expiring_within_days");
        var low = int.TryParse(lowRaw, out var ln) && ln >= 0 ? ln : 5;
        var exp = int.TryParse(expRaw, out var en) && en >= 1 ? en : 30;
        return new InventorySettingsDto
        {
            LowStockThreshold = low,
            ExpiringWithinDays = exp
        };
    }

    public async Task<SettingsSaveResult> SaveInventoryAsync(InventorySettingsDto model, CancellationToken ct = default)
    {
        logger.LogInformation(
            "SaveInventorySettings Start LowStock={Low} ExpiringDays={Days}",
            model.LowStockThreshold, model.ExpiringWithinDays);
        try
        {
            var errors = new Dictionary<string, string>();
            if (model.LowStockThreshold < 0)
                errors[nameof(model.LowStockThreshold)] = "Ngưỡng tồn thấp phải ≥ 0.";
            if (model.ExpiringWithinDays < 1)
                errors[nameof(model.ExpiringWithinDays)] = "Số ngày sắp hết hạn phải ≥ 1.";
            if (errors.Count > 0)
            {
                logger.LogWarning("SaveInventorySettings Done rejected");
                return new SettingsSaveResult(false, errors);
            }

            await UpsertManyAsync("inventory", new Dictionary<string, string>
            {
                ["inventory.low_stock_threshold"] = model.LowStockThreshold.ToString(),
                ["inventory.expiring_within_days"] = model.ExpiringWithinDays.ToString()
            }, null, ct);

            logger.LogInformation(
                "SaveInventorySettings Done LowStock={Low} ExpiringDays={Days}",
                model.LowStockThreshold, model.ExpiringWithinDays);
            return new SettingsSaveResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveInventorySettings Error");
            throw;
        }
    }

    private async Task<Dictionary<string, string?>> LoadMapAsync(string group, CancellationToken ct)
        => await db.SiteSettings.AsNoTracking()
            .Where(s => s.Group == group)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    private async Task UpsertManyAsync(
        string group,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<string>? removeKeys,
        CancellationToken ct)
    {
        var keys = values.Keys.Concat(removeKeys ?? []).Distinct().ToArray();
        var existing = await db.SiteSettings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);
        foreach (var pair in values)
        {
            if (existing.TryGetValue(pair.Key, out var row))
            {
                row.Value = pair.Value;
                row.Group = group;
            }
            else
            {
                db.SiteSettings.Add(new SiteSetting { Key = pair.Key, Value = pair.Value, Group = group });
            }
        }
        if (removeKeys is not null)
            foreach (var key in removeKeys)
                if (existing.TryGetValue(key, out var row))
                    db.SiteSettings.Remove(row);
        await db.SaveChangesAsync(ct);
        chrome.InvalidateSettings();
    }

    private EmailSettingsDto BuildEmailModel(
        IReadOnlyDictionary<string, string?> map,
        IReadOnlyDictionary<string, string?> company)
    {
        var companyEmail = company.GetValueOrDefault("company.email") ?? "";
        var model = new EmailSettingsDto
        {
            OrderEmail = map.GetValueOrDefault("notifications.order_email") ?? companyEmail,
            InquiryEmail = map.GetValueOrDefault("notifications.inquiry_email") ?? companyEmail,
            ApplicationEmail = map.GetValueOrDefault("notifications.application_email") ?? companyEmail,
            ServiceBookingEmail = map.GetValueOrDefault("notifications.service_booking_email") ?? companyEmail,
            DealerEmail = map.GetValueOrDefault("notifications.dealer_email") ?? companyEmail,
            CompanyEmail = companyEmail,
            TestTo = companyEmail
        };
        FillSmtpStatus(model);
        return model;
    }

    private void FillSmtpStatus(EmailSettingsDto model)
    {
        model.SmtpEnabled = config.GetValue("Email:Smtp:Enabled", false);
        model.SmtpHost = config["Email:Smtp:Host"] ?? "";
        model.SmtpPort = config.GetValue("Email:Smtp:Port", 587);
        model.SmtpFrom = config["Email:Smtp:From"] ?? config["Email:Smtp:User"] ?? "";
        model.SmtpFromName = config["Email:Smtp:FromName"] ?? "Harian";
        model.SmtpUseSsl = config.GetValue("Email:Smtp:UseSsl", true);
    }

    private static Dictionary<string, string> ValidateEmails(EmailSettingsDto model)
    {
        var errors = new Dictionary<string, string>();
        AddIfInvalid(errors, nameof(model.OrderEmail), model.OrderEmail, "Email đơn hàng không hợp lệ.");
        AddIfInvalid(errors, nameof(model.InquiryEmail), model.InquiryEmail, "Email liên hệ không hợp lệ.");
        AddIfInvalid(errors, nameof(model.ApplicationEmail), model.ApplicationEmail, "Email tuyển dụng không hợp lệ.");
        AddIfInvalid(errors, nameof(model.ServiceBookingEmail), model.ServiceBookingEmail, "Email đặt lịch không hợp lệ.");
        AddIfInvalid(errors, nameof(model.DealerEmail), model.DealerEmail, "Email đại lý không hợp lệ.");
        return errors;
    }

    private static void AddIfInvalid(IDictionary<string, string> errors, string key, string value, string message)
    {
        if (!GuestValidation.IsEmail(value))
            errors[key] = message;
    }
}
