using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Email;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Email;

public sealed partial class EmailTemplateService(AppDbContext db, ILogger<EmailTemplateService> logger) : IEmailTemplateService
{
    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    public async Task<IReadOnlyList<EmailTemplateListItemDto>> ListAsync(CancellationToken ct = default)
    {
        return await db.EmailTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new EmailTemplateListItemDto(t.Id, t.Code, t.Name, t.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<EmailTemplateEditDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var t = await db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null
            ? null
            : new EmailTemplateEditDto(t.Id, t.Code, t.Name, t.PlaceholdersHelp, t.SubjectTemplate, t.BodyHtml);
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(EmailTemplateSaveRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("SaveEmailTemplate Start Id={Id}", request.Id);
        try
        {
            var t = await db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
            if (t is null)
            {
                logger.LogWarning("SaveEmailTemplate Done rejected Id={Id} Error={Error}", request.Id, "Không tìm thấy.");
                return (false, "Không tìm thấy mẫu email.");
            }

            var subject = (request.SubjectTemplate ?? "").Trim();
            if (string.IsNullOrWhiteSpace(subject) || subject.Length > 300)
            {
                logger.LogWarning("SaveEmailTemplate Done rejected Id={Id} Error={Error}", request.Id, "subject");
                return (false, "Tiêu đề bắt buộc, tối đa 300 ký tự.");
            }

            var body = request.BodyHtml?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(body))
            {
                logger.LogWarning("SaveEmailTemplate Done rejected Id={Id} Error={Error}", request.Id, "body");
                return (false, "Nội dung email không được trống.");
            }

            t.SubjectTemplate = subject;
            t.BodyHtml = body;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveEmailTemplate Done Id={Id} Code={Code}", t.Id, t.Code);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveEmailTemplate Error Id={Id}", request.Id);
            throw;
        }
    }

    public async Task<(string Subject, string Body)> RenderAsync(
        string code,
        IReadOnlyDictionary<string, string?> vars,
        CancellationToken ct = default)
    {
        var t = await db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        var subjectTpl = t?.SubjectTemplate;
        var bodyTpl = t?.BodyHtml;

        if (string.IsNullOrWhiteSpace(subjectTpl) || string.IsNullOrWhiteSpace(bodyTpl))
        {
            var fallback = EmailTemplateDefaults.Get(code);
            subjectTpl = string.IsNullOrWhiteSpace(subjectTpl) ? fallback.Subject : subjectTpl;
            bodyTpl = string.IsNullOrWhiteSpace(bodyTpl) ? fallback.Body : bodyTpl;
        }

        return (Apply(subjectTpl!, vars), Apply(bodyTpl!, vars));
    }

    public static string Apply(string template, IReadOnlyDictionary<string, string?> vars)
    {
        return PlaceholderRegex().Replace(template, m =>
        {
            var key = m.Groups["key"].Value;
            if (!vars.TryGetValue(key, out var value) || value is null)
                return "";
            return value;
        });
    }

    public static string Enc(string? value) => WebUtility.HtmlEncode(value ?? "");
}
