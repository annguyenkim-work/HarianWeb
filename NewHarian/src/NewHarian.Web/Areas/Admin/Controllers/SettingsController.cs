using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Payments;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class SettingsController(
    AppDbContext db,
    IMediaStorage media,
    ConfigurableEmailSender email,
    IConfiguration config,
    IVietQrService vietQr,
    ILogger<SettingsController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var map = await LoadMapAsync(ct, "company");
        var vm = new BankSettingsVm
        {
            BankBin = map.GetValueOrDefault("company.bank.bin") ?? "",
            BankName = map.GetValueOrDefault("company.bank.name") ?? "",
            BankAccount = map.GetValueOrDefault("company.bank.account") ?? "",
            AccountHolderName = map.GetValueOrDefault("company.bank.account_name") ?? "",
            BankBranch = map.GetValueOrDefault("company.bank.branch") ?? ""
        };
        vm.VietQrPreviewDataUrl = BuildPreviewQr(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BankSettingsVm model, CancellationToken ct)
    {
        logger.LogInformation("SaveBankSettings Start");
        try
        {
            var bin = (model.BankBin ?? "").Trim();
            if (!string.IsNullOrEmpty(bin) && VnBankCatalog.FindByBin(bin) is { } bank)
                model.BankName = bank.DisplayName;

            await UpsertAsync("company.bank.bin", bin, "company", ct);
            await UpsertAsync("company.bank.name", (model.BankName ?? "").Trim(), "company", ct);
            await UpsertAsync("company.bank.account", (model.BankAccount ?? "").Trim(), "company", ct);
            await UpsertAsync("company.bank.account_name", (model.AccountHolderName ?? "").Trim(), "company", ct);
            await UpsertAsync("company.bank.branch", model.BankBranch ?? "", "company", ct);
            await RemoveSettingAsync("company.bank.qr", ct);

            logger.LogInformation("SaveBankSettings Done");
            TempData["Success"] = "Đã lưu thông tin ngân hàng / VietQR.";
            return RedirectToAction(nameof(Index), new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveBankSettings Error");
            throw;
        }
    }

    private string? BuildPreviewQr(BankSettingsVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.BankBin) || string.IsNullOrWhiteSpace(vm.BankAccount))
            return null;
        return vietQr.CreatePngDataUrl(vm.BankBin, vm.BankAccount, 10000, "HAR-ORDER-TEST", vm.AccountHolderName);
    }

    public async Task<IActionResult> Brand(CancellationToken ct)
    {
        var map = await LoadMapAsync(ct, "company");
        return View(new BrandSettingsVm
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
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Brand(BrandSettingsVm model, IFormFile? logoFile, CancellationToken ct)
    {
        logger.LogInformation("SaveBrandSettings Start");
        try
        {
            await UpsertAsync("company.brand", (model.BrandName ?? "").Trim(), "company", ct);
            await UpsertAsync("company.name", (model.CompanyName ?? "").Trim(), "company", ct);
            await UpsertAsync("company.phone", (model.Phone ?? "").Trim(), "company", ct);
            await UpsertAsync("company.phone2", (model.Phone2 ?? "").Trim(), "company", ct);
            await UpsertAsync("company.email", (model.Email ?? "").Trim(), "company", ct);
            await UpsertAsync("company.address", (model.Address ?? "").Trim(), "company", ct);
            await UpsertAsync("company.tagline.vi", (model.TaglineVi ?? "").Trim(), "company", ct);
            await UpsertAsync("company.tagline.en", (model.TaglineEn ?? "").Trim(), "company", ct);
            await UpsertAsync("company.tagline.ja", (model.TaglineJa ?? "").Trim(), "company", ct);
            await UpsertAsync("company.facebook", (model.FacebookUrl ?? "").Trim(), "company", ct);
            await UpsertAsync("company.instagram", (model.InstagramUrl ?? "").Trim(), "company", ct);

            if (logoFile is { Length: > 0 })
            {
                await using var stream = logoFile.OpenReadStream();
                var uploaded = await media.SaveImageAsync(stream, logoFile.FileName, logoFile.ContentType, User.Identity?.Name, ct, "brand");
                model.LogoUrl = uploaded.Url;
                await UpsertAsync("company.logo", uploaded.Url, "company", ct);
            }
            else if (!string.IsNullOrWhiteSpace(model.LogoUrl))
            {
                await UpsertAsync("company.logo", model.LogoUrl.Trim(), "company", ct);
            }

            logger.LogInformation("SaveBrandSettings Done");
            TempData["Success"] = "Đã lưu thương hiệu, footer và giao diện header.";
            return RedirectToAction(nameof(Brand), new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveBrandSettings Error");
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearLogo(CancellationToken ct)
    {
        logger.LogInformation("ClearLogo Start");
        try
        {
            await UpsertAsync("company.logo", "", "company", ct);
            logger.LogInformation("ClearLogo Done");
            TempData["Success"] = "Đã gỡ logo. Header sẽ hiện tên thương hiệu dạng chữ.";
            return RedirectToAction(nameof(Brand), new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ClearLogo Error");
            throw;
        }
    }

    public async Task<IActionResult> Email(CancellationToken ct)
    {
        var map = await LoadMapAsync(ct, "notifications");
        var company = await LoadMapAsync(ct, "company");
        return View(BuildEmailVm(map, company));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Email(EmailSettingsVm model, CancellationToken ct)
    {
        logger.LogInformation("SaveEmailSettings Start");
        try
        {
            if (!ValidateNotificationEmails(model))
            {
                logger.LogWarning("SaveEmailSettings Done rejected");
                var company = await LoadMapAsync(ct, "company");
                model.CompanyEmail = company.GetValueOrDefault("company.email") ?? "";
                FillSmtpStatus(model);
                return View(model);
            }

            await UpsertAsync("notifications.order_email", model.OrderEmail.Trim(), "notifications", ct);
            await UpsertAsync("notifications.inquiry_email", model.InquiryEmail.Trim(), "notifications", ct);
            await UpsertAsync("notifications.application_email", model.ApplicationEmail.Trim(), "notifications", ct);
            await UpsertAsync("notifications.service_booking_email", model.ServiceBookingEmail.Trim(), "notifications", ct);

            logger.LogInformation("SaveEmailSettings Done");
            TempData["Success"] = "Đã lưu email nhận thông báo.";
            return RedirectToAction(nameof(Email), new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveEmailSettings Error");
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestEmail(string? testTo, CancellationToken ct)
    {
        logger.LogInformation("TestEmail Start To={To}", testTo);
        try
        {
            var to = (testTo ?? "").Trim();
            if (!GuestValidation.IsEmail(to))
            {
                logger.LogWarning("TestEmail Done rejected Error={Error}", "invalid to");
                TempData["Error"] = "Email nhận thử không hợp lệ.";
                return RedirectToAction(nameof(Email), new { area = "Admin" });
            }

            var smtpOn = config.GetValue("Email:Smtp:Enabled", false);
            var subject = "[Harian] Email thử nghiệm";
            var body =
                $"<p>Đây là email thử từ Admin - Harian.</p>" +
                $"<p>SMTP Enabled = <b>{(smtpOn ? "true" : "false")}</b></p>" +
                $"<p>Thời gian (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>" +
                (smtpOn
                    ? "<p>Nếu bạn nhận được thư này trong hộp thư, SMTP đã hoạt động.</p>"
                    : "<p>SMTP đang tắt - thư chỉ được ghi vào <code>App_Data/outbox</code> trên server.</p>");

            await email.SendAsync(to, subject, body, ct);
            logger.LogInformation("TestEmail Done To={To} SmtpEnabled={Smtp}", to, smtpOn);
            TempData["Success"] = smtpOn
                ? $"Đã gửi thử tới {to}. Kiểm tra hộp thư (và thư mục spam)."
                : $"SMTP đang tắt - đã ghi file outbox cho {to}. Bật Email:Smtp:Enabled trong appsettings để gửi thật.";
            return RedirectToAction(nameof(Email), new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TestEmail Error To={To}", testTo);
            TempData["Error"] = "Gửi thử thất bại: " + ex.Message;
            return RedirectToAction(nameof(Email), new { area = "Admin" });
        }
    }

    private EmailSettingsVm BuildEmailVm(Dictionary<string, string?> map, Dictionary<string, string?> company)
    {
        var companyEmail = company.GetValueOrDefault("company.email") ?? "";
        var vm = new EmailSettingsVm
        {
            OrderEmail = map.GetValueOrDefault("notifications.order_email") ?? companyEmail,
            InquiryEmail = map.GetValueOrDefault("notifications.inquiry_email") ?? companyEmail,
            ApplicationEmail = map.GetValueOrDefault("notifications.application_email") ?? companyEmail,
            ServiceBookingEmail = map.GetValueOrDefault("notifications.service_booking_email") ?? companyEmail,
            CompanyEmail = companyEmail,
            TestTo = companyEmail
        };
        FillSmtpStatus(vm);
        return vm;
    }

    private void FillSmtpStatus(EmailSettingsVm vm)
    {
        vm.SmtpEnabled = config.GetValue("Email:Smtp:Enabled", false);
        vm.SmtpHost = config["Email:Smtp:Host"] ?? "";
        vm.SmtpPort = config.GetValue("Email:Smtp:Port", 587);
        vm.SmtpFrom = config["Email:Smtp:From"] ?? config["Email:Smtp:User"] ?? "";
        vm.SmtpFromName = config["Email:Smtp:FromName"] ?? "Harian";
        vm.SmtpUseSsl = config.GetValue("Email:Smtp:UseSsl", true);
    }

    private bool ValidateNotificationEmails(EmailSettingsVm model)
    {
        var ok = true;
        if (!GuestValidation.IsEmail(model.OrderEmail))
        {
            ModelState.AddModelError(nameof(model.OrderEmail), "Email đơn hàng không hợp lệ.");
            ok = false;
        }
        if (!GuestValidation.IsEmail(model.InquiryEmail))
        {
            ModelState.AddModelError(nameof(model.InquiryEmail), "Email liên hệ không hợp lệ.");
            ok = false;
        }
        if (!GuestValidation.IsEmail(model.ApplicationEmail))
        {
            ModelState.AddModelError(nameof(model.ApplicationEmail), "Email tuyển dụng không hợp lệ.");
            ok = false;
        }
        if (!GuestValidation.IsEmail(model.ServiceBookingEmail))
        {
            ModelState.AddModelError(nameof(model.ServiceBookingEmail), "Email đặt lịch không hợp lệ.");
            ok = false;
        }
        return ok;
    }

    private async Task<Dictionary<string, string?>> LoadMapAsync(CancellationToken ct, string group)
    {
        return await db.SiteSettings.AsNoTracking()
            .Where(s => s.Group == group)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
    }

    private async Task UpsertAsync(string key, string value, string group, CancellationToken ct)
    {
        var row = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
            db.SiteSettings.Add(new SiteSetting { Key = key, Value = value, Group = group });
        else
        {
            row.Value = value;
            row.Group = group;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RemoveSettingAsync(string key, CancellationToken ct)
    {
        var row = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) return;
        db.SiteSettings.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public class BankSettingsVm
    {
        public string BankBin { get; set; } = "";
        public string BankName { get; set; } = "";
        public string BankAccount { get; set; } = "";
        public string AccountHolderName { get; set; } = "";
        public string BankBranch { get; set; } = "";
        public string? VietQrPreviewDataUrl { get; set; }
    }

    public class BrandSettingsVm
    {
        public string BrandName { get; set; } = "Harian";
        public string CompanyName { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Phone2 { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public string TaglineVi { get; set; } = "";
        public string TaglineEn { get; set; } = "";
        public string TaglineJa { get; set; } = "";
        public string FacebookUrl { get; set; } = "";
        public string InstagramUrl { get; set; } = "";
    }

    public class EmailSettingsVm
    {
        public string OrderEmail { get; set; } = "";
        public string InquiryEmail { get; set; } = "";
        public string ApplicationEmail { get; set; } = "";
        public string ServiceBookingEmail { get; set; } = "";
        public string CompanyEmail { get; set; } = "";
        public string TestTo { get; set; } = "";

        public bool SmtpEnabled { get; set; }
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpFrom { get; set; } = "";
        public string SmtpFromName { get; set; } = "Harian";
        public bool SmtpUseSsl { get; set; } = true;
    }
}
