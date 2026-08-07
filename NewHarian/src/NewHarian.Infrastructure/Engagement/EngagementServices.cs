using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Email;
using NewHarian.Application.Engagement;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Engagement;

public sealed class InquiryService(
    AppDbContext db,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    IAuditService audit,
    IAdminNotificationService notifications,
    ILogger<InquiryService> logger) : IInquiryService
{
    public async Task<(bool Ok, string? Error, int? Id)> SubmitAsync(ContactFormModel model, string lang, CancellationToken ct = default)
    {
        logger.LogInformation("SubmitInquiry Start");
        try
        {
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                logger.LogInformation("SubmitInquiry Done honeypot");
                return (true, null, null); // honeypot - silent success
            }

            var err = ValidateContact(model);
            if (err is not null)
            {
                logger.LogWarning("SubmitInquiry Done rejected Error={Error}", err);
                return (false, err, null);
            }

            var entity = new Inquiry
            {
                Name = model.Name.Trim(),
                Email = model.Email.Trim(),
                Phone = NullIfEmpty(model.Phone),
                Address = NullIfEmpty(model.Address),
                Subject = "Liên hệ từ website",
                Message = model.Message.Trim(),
                Status = InquiryStatus.New,
                LanguageCode = lang is "en" or "ja" ? lang : "vi",
                CreatedAt = DateTime.UtcNow
            };
            db.Inquiries.Add(entity);
            await db.SaveChangesAsync(ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.InquiryCreated,
                $"Liên hệ mới từ {entity.Name}",
                entity.Email,
                $"/admin/Inquiries?q={Uri.EscapeDataString(entity.Email)}",
                "Inquiry",
                entity.Id.ToString(),
                ct);

            var staff = await GetSettingAsync("notifications.inquiry_email", ct)
                        ?? await GetSettingAsync("company.email", ct)
                        ?? "info@harian.local";
            try
            {
                var staffMail = await emailTemplates.RenderAsync(EmailTemplateCodes.InquiryStaff, new Dictionary<string, string?>
                {
                    ["InquiryId"] = entity.Id.ToString(),
                    ["CustomerName"] = EmailTemplateService.Enc(entity.Name),
                    ["CustomerEmail"] = EmailTemplateService.Enc(entity.Email),
                    ["CustomerPhone"] = EmailTemplateService.Enc(entity.Phone),
                    ["Message"] = EmailTemplateService.Enc(entity.Message)
                }, ct);
                await email.SendAsync(staff, staffMail.Subject, staffMail.Body, ct);

                var customerMail = await emailTemplates.RenderAsync(EmailTemplateCodes.InquiryCustomer, new Dictionary<string, string?>
                {
                    ["CustomerName"] = EmailTemplateService.Enc(entity.Name)
                }, ct);
                await email.SendAsync(entity.Email, customerMail.Subject, customerMail.Body, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inquiry email failed for {Id}", entity.Id);
            }

            logger.LogInformation("SubmitInquiry Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SubmitInquiry Error");
            throw;
        }
    }

    public async Task<IReadOnlyList<InquiryListItemDto>> ListAsync(
        InquiryStatus? status,
        string? q = null,
        string? sort = null,
        string? dir = null,
        CancellationToken ct = default)
    {
        var query = db.Inquiries.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(term) ||
                i.Email.ToLower().Contains(term) ||
                (i.Phone != null && i.Phone.ToLower().Contains(term)));
        }

        var sortKey = AdminListQuery.NormalizeSort(sort, InquirySortKeys, "createdAt");
        var sortDir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sortKey));
        var asc = AdminListQuery.IsAsc(sortDir);

        query = (sortKey, asc) switch
        {
            ("id", true) => query.OrderBy(i => i.Id),
            ("id", false) => query.OrderByDescending(i => i.Id),
            ("name", true) => query.OrderBy(i => i.Name).ThenByDescending(i => i.Id),
            ("name", false) => query.OrderByDescending(i => i.Name).ThenByDescending(i => i.Id),
            ("email", true) => query.OrderBy(i => i.Email).ThenByDescending(i => i.Id),
            ("email", false) => query.OrderByDescending(i => i.Email).ThenByDescending(i => i.Id),
            ("phone", true) => query.OrderBy(i => i.Phone).ThenByDescending(i => i.Id),
            ("phone", false) => query.OrderByDescending(i => i.Phone).ThenByDescending(i => i.Id),
            ("status", true) => query.OrderBy(i => i.Status).ThenByDescending(i => i.Id),
            ("status", false) => query.OrderByDescending(i => i.Status).ThenByDescending(i => i.Id),
            ("createdAt", true) => query.OrderBy(i => i.CreatedAt).ThenByDescending(i => i.Id),
            _ => query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id),
        };

        return await query
            .Select(i => new InquiryListItemDto(i.Id, i.CreatedAt, i.Name, i.Email, i.Phone, i.Status, i.HandledByUserId))
            .ToListAsync(ct);
    }

    private static readonly HashSet<string> InquirySortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "email", "phone", "status"
    };

    public async Task<InquiryDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var i = await db.Inquiries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return i is null ? null : new InquiryDetailDto(
            i.Id, i.CreatedAt, i.Name, i.Email, i.Phone, i.Address, i.Subject, i.Message,
            i.Status, i.InternalNotes, i.HandledByUserId, i.LanguageCode, i.ResolvedAt);
    }

    public async Task<(bool Ok, string? Error)> UpdateStatusAsync(int id, InquiryStatus status, string? notes, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("UpdateInquiryStatus Start Id={Id} Status={Status}", id, status);
        try
        {
            var i = await db.Inquiries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (i is null)
            {
                logger.LogWarning("UpdateInquiryStatus Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            var from = i.Status;
            i.Status = status;
            if (!string.IsNullOrWhiteSpace(notes)) i.InternalNotes = notes.Trim();
            i.HandledByUserId = userId;
            if (status is InquiryStatus.Resolved or InquiryStatus.Closed)
                i.ResolvedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "Inquiry.StatusChanged",
                "Inquiry",
                i.Id.ToString(),
                new { Status = from.ToString() },
                new { Status = status.ToString(), InternalNotes = notes },
                ct);
            logger.LogInformation("UpdateInquiryStatus Done Id={Id} Status={Status}", id, status);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateInquiryStatus Error Id={Id}", id);
            throw;
        }
    }

    private static string? ValidateContact(ContactFormModel m)
    {
        if (!GuestValidation.HasLength(m.Name, 2, GuestValidation.NameMax)) return "Vui lòng nhập họ tên (2-200 ký tự)."; // CNT_REQUIRED
        if (!GuestValidation.IsEmail(m.Email)) return "Email không hợp lệ."; // CNT_EMAIL_INVALID
        if (!GuestValidation.IsPhone(m.Phone)) return "Số điện thoại không hợp lệ."; // CNT_PHONE_INVALID
        if (!GuestValidation.FitsMax(m.Address, GuestValidation.AddressMax)) return "Địa chỉ tối đa 500 ký tự.";
        var msg = m.Message?.Trim() ?? "";
        if (msg.Length < 10) return "Nội dung tối thiểu 10 ký tự."; // CNT_MESSAGE_TOO_SHORT
        if (msg.Length > GuestValidation.MessageMax) return "Nội dung tối đa 5000 ký tự."; // CNT_MESSAGE_TOO_LONG
        return null;
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed class JobApplicationService(
    AppDbContext db,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    IAuditService audit,
    IMediaStorage mediaStorage,
    IAdminNotificationService notifications,
    ILogger<JobApplicationService> logger) : IJobApplicationService
{
    public async Task<(bool Ok, string? Error, int? Id)> SubmitAsync(CareerFormModel model, string lang, CancellationToken ct = default)
    {
        logger.LogInformation("SubmitApplication Start SitePostId={SitePostId}", model.SitePostId);
        try
        {
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                logger.LogInformation("SubmitApplication Done honeypot");
                return (true, null, null);
            }

            var err = Validate(model);
            if (err is not null)
            {
                logger.LogWarning("SubmitApplication Done rejected Error={Error}", err);
                return (false, err, null);
            }

            if (model.SitePostId is not int postId || postId <= 0)
            {
                logger.LogWarning("SubmitApplication Done rejected Error={Error}", "Vui lòng ứng tuyển từ một tin tuyển dụng.");
                return (false, "Vui lòng ứng tuyển từ một tin tuyển dụng.", null);
            }

            var post = await db.SitePosts.AsNoTracking()
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == postId && p.Kind == PostKind.Job && p.IsPublished, ct);
            if (post is null)
            {
                logger.LogWarning("SubmitApplication Done rejected Error={Error}", "Tin tuyển dụng không còn hiệu lực.");
                return (false, "Tin tuyển dụng không còn hiệu lực.", null);
            }

            var entity = new JobApplication
            {
                SitePostId = post.Id,
                ApplicationType = model.ApplicationType,
                Gender = NullIfEmpty(model.Gender),
                FullName = model.FullName.Trim(),
                Age = model.Age,
                Prefecture = NullIfEmpty(model.Prefecture),
                City = NullIfEmpty(model.City),
                Address = NullIfEmpty(model.Address),
                Phone = NullIfEmpty(model.Phone),
                Email = model.Email.Trim(),
                Message = model.Message?.Trim() ?? "",
                AttachmentMediaFileId = model.AttachmentMediaFileId,
                Status = ApplicationStatus.New,
                LanguageCode = lang is "en" or "ja" ? lang : "vi",
                CreatedAt = DateTime.UtcNow
            };
            db.JobApplications.Add(entity);
            await db.SaveChangesAsync(ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.ApplicationCreated,
                $"Hồ sơ ứng tuyển mới - {entity.FullName}",
                entity.Email,
                $"/admin/Applications?q={Uri.EscapeDataString(entity.Email)}",
                "JobApplication",
                entity.Id.ToString(),
                ct);

            var staff = await GetSettingAsync("notifications.application_email", ct)
                        ?? await GetSettingAsync("company.email", ct)
                        ?? "info@harian.local";
            var jobTitle = post.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Title ?? post.Slug;
            try
            {
                var staffMail = await emailTemplates.RenderAsync(EmailTemplateCodes.ApplicationStaff, new Dictionary<string, string?>
                {
                    ["ApplicationId"] = entity.Id.ToString(),
                    ["CustomerName"] = EmailTemplateService.Enc(entity.FullName),
                    ["CustomerEmail"] = EmailTemplateService.Enc(entity.Email),
                    ["ApplicationType"] = EmailTemplateService.Enc(entity.ApplicationType.ToString()),
                    ["JobTitle"] = EmailTemplateService.Enc(jobTitle)
                }, ct);

                IReadOnlyList<EmailAttachment>? cvAttachments = null;
                if (entity.AttachmentMediaFileId is int mediaId)
                {
                    var attachment = await TryBuildCvAttachmentAsync(mediaId, ct);
                    if (attachment is not null)
                        cvAttachments = [attachment];
                    else
                        logger.LogWarning("Application CV file missing for email Id={Id} MediaId={MediaId}",
                            entity.Id, mediaId);
                }

                await email.SendAsync(staff, staffMail.Subject, staffMail.Body, ct, cvAttachments);

                var customerMail = await emailTemplates.RenderAsync(EmailTemplateCodes.ApplicationCustomer, new Dictionary<string, string?>
                {
                    ["CustomerName"] = EmailTemplateService.Enc(entity.FullName),
                    ["JobTitle"] = EmailTemplateService.Enc(jobTitle)
                }, ct);
                await email.SendAsync(entity.Email, customerMail.Subject, customerMail.Body, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Application email failed for {Id}", entity.Id);
            }

            logger.LogInformation("SubmitApplication Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SubmitApplication Error SitePostId={SitePostId}", model.SitePostId);
            throw;
        }
    }

    public async Task<IReadOnlyList<ApplicationListItemDto>> ListAsync(
        ApplicationStatus? status,
        int? sitePostId,
        string? q = null,
        string? sort = null,
        string? dir = null,
        CancellationToken ct = default)
    {
        var query = db.JobApplications.AsNoTracking()
            .Include(a => a.SitePost).ThenInclude(p => p!.Translations)
            .AsQueryable();
        if (status is not null) query = query.Where(a => a.Status == status);
        if (sitePostId is int pid) query = query.Where(a => a.SitePostId == pid);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(a =>
                a.FullName.ToLower().Contains(term) ||
                a.Email.ToLower().Contains(term) ||
                (a.Phone != null && a.Phone.ToLower().Contains(term)));
        }

        var sortKey = AdminListQuery.NormalizeSort(sort, ApplicationSortKeys, "createdAt");
        var sortDir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sortKey));
        var asc = AdminListQuery.IsAsc(sortDir);

        query = (sortKey, asc) switch
        {
            ("id", true) => query.OrderBy(a => a.Id),
            ("id", false) => query.OrderByDescending(a => a.Id),
            ("name", true) => query.OrderBy(a => a.FullName).ThenByDescending(a => a.Id),
            ("name", false) => query.OrderByDescending(a => a.FullName).ThenByDescending(a => a.Id),
            ("email", true) => query.OrderBy(a => a.Email).ThenByDescending(a => a.Id),
            ("email", false) => query.OrderByDescending(a => a.Email).ThenByDescending(a => a.Id),
            ("job", true) => query
                .OrderBy(a => a.SitePost != null
                    ? (a.SitePost.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Title).FirstOrDefault() ?? a.SitePost.Slug)
                    : "")
                .ThenByDescending(a => a.Id),
            ("job", false) => query
                .OrderByDescending(a => a.SitePost != null
                    ? (a.SitePost.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Title).FirstOrDefault() ?? a.SitePost.Slug)
                    : "")
                .ThenByDescending(a => a.Id),
            ("type", true) => query.OrderBy(a => a.ApplicationType).ThenByDescending(a => a.Id),
            ("type", false) => query.OrderByDescending(a => a.ApplicationType).ThenByDescending(a => a.Id),
            ("status", true) => query.OrderBy(a => a.Status).ThenByDescending(a => a.Id),
            ("status", false) => query.OrderByDescending(a => a.Status).ThenByDescending(a => a.Id),
            ("hasCv", true) => query.OrderBy(a => a.AttachmentMediaFileId != null).ThenByDescending(a => a.Id),
            ("hasCv", false) => query.OrderByDescending(a => a.AttachmentMediaFileId != null).ThenByDescending(a => a.Id),
            ("createdAt", true) => query.OrderBy(a => a.CreatedAt).ThenByDescending(a => a.Id),
            _ => query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id),
        };

        var list = await query.ToListAsync(ct);
        return list.Select(a =>
        {
            var title = a.SitePost?.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Title
                        ?? a.SitePost?.Slug;
            return new ApplicationListItemDto(
                a.Id, a.CreatedAt, a.FullName, a.Email, a.ApplicationType, a.Status,
                a.AttachmentMediaFileId != null, a.SitePostId, title);
        }).ToList();
    }

    private static readonly HashSet<string> ApplicationSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "email", "job", "type", "status", "hasCv"
    };

    public async Task<ApplicationDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var a = await db.JobApplications.AsNoTracking()
            .Include(x => x.Attachment)
            .Include(x => x.SitePost).ThenInclude(p => p!.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return null;
        var jobTitle = a.SitePost?.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Title
                       ?? a.SitePost?.Slug;
        // Never expose filesystem / public static URL for private CVs - use auth-gated download.
        var cvUrl = a.AttachmentMediaFileId is > 0
            ? $"/admin/Applications/Cv/{a.Id}"
            : null;
        return new ApplicationDetailDto(
            a.Id, a.CreatedAt, a.ApplicationType, a.Gender, a.FullName, a.Age,
            a.Prefecture, a.City, a.Address, a.Phone, a.Email, a.Message,
            a.Status, a.InternalNotes, a.ReviewedByUserId, a.LanguageCode, a.ReviewedAt,
            cvUrl, a.Attachment?.FileName,
            a.SitePostId, jobTitle, a.SitePost?.Slug);
    }

    public async Task<(bool Ok, string? Error)> UpdateStatusAsync(int id, ApplicationStatus status, string? notes, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("UpdateApplicationStatus Start Id={Id} Status={Status}", id, status);
        try
        {
            var a = await db.JobApplications.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null)
            {
                logger.LogWarning("UpdateApplicationStatus Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            var from = a.Status;
            a.Status = status;
            if (!string.IsNullOrWhiteSpace(notes)) a.InternalNotes = notes.Trim();
            a.ReviewedByUserId = userId;
            a.ReviewedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "Application.StatusChanged",
                "JobApplication",
                a.Id.ToString(),
                new { Status = from.ToString() },
                new { Status = status.ToString(), InternalNotes = notes },
                ct);
            logger.LogInformation("UpdateApplicationStatus Done Id={Id} Status={Status}", id, status);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateApplicationStatus Error Id={Id}", id);
            throw;
        }
    }

    private static string? Validate(CareerFormModel m)
    {
        if (!GuestValidation.HasLength(m.FullName, 2, GuestValidation.NameMax)) return "Vui lòng nhập họ tên (2-200 ký tự)."; // APP_REQUIRED
        if (m.Age is int age && age is < 16 or > 99) return "Tuổi phải từ 16 đến 99."; // APP_AGE_INVALID
        if (!GuestValidation.FitsMax(m.Prefecture, 100)) return "Tỉnh/Prefecture tối đa 100 ký tự.";
        if (!GuestValidation.FitsMax(m.City, 100)) return "Thành phố tối đa 100 ký tự.";
        if (!GuestValidation.FitsMax(m.Address, GuestValidation.AddressMax)) return "Địa chỉ tối đa 500 ký tự.";
        if (!GuestValidation.IsPhone(m.Phone)) return "Số điện thoại không hợp lệ."; // APP_PHONE_INVALID
        if (!GuestValidation.IsEmail(m.Email)) return "Email không hợp lệ."; // APP_EMAIL_INVALID
        var msg = m.Message?.Trim() ?? "";
        if (msg.Length > GuestValidation.MessageMax) return "Note tối đa 5000 ký tự.";
        if (m.ApplicationType == ApplicationType.Application && m.AttachmentMediaFileId is null or <= 0)
            return "Vui lòng đính kèm CV khi ứng tuyển."; // APP_CV_REQUIRED
        return null;
    }

    private async Task<EmailAttachment?> TryBuildCvAttachmentAsync(int mediaId, CancellationToken ct)
    {
        await using var opened = await mediaStorage.OpenAsync(mediaId, ct);
        if (opened is null) return null;

        using var ms = new MemoryStream();
        await opened.Content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        if (bytes.Length == 0) return null;

        var contentType = string.IsNullOrWhiteSpace(opened.ContentType)
            ? "application/octet-stream"
            : opened.ContentType;
        return new EmailAttachment(opened.DownloadFileName, contentType, bytes);
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
