using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Email;

/// <summary>Enqueues mail to DB; <see cref="EmailOutboxHostedService"/> sends in background.</summary>
public sealed class QueuingEmailSender(
    AppDbContext db,
    IWebHostEnvironment env,
    ILogger<QueuingEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Email recipient is required.", nameof(to));

        var msg = new EmailOutboxMessage
        {
            ToAddress = to.Trim(),
            Subject = subject?.Trim() ?? "",
            HtmlBody = htmlBody ?? "",
            Status = EmailOutboxStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
            MaxAttempts = 5
        };

        db.EmailOutboxMessages.Add(msg);
        await db.SaveChangesAsync(cancellationToken);

        if (attachments is { Count: > 0 })
        {
            try
            {
                msg.AttachmentsJson = await PersistAttachmentsAsync(msg.Id, attachments, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email queue attachment persist failed Id={Id}", msg.Id);
                msg.LastError = "Attachment persist failed: " + ex.Message;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        logger.LogInformation("Email queued Id={Id} To={To} Subject={Subject}", msg.Id, msg.ToAddress, msg.Subject);
    }

    private async Task<string> PersistAttachmentsAsync(
        long messageId,
        IReadOnlyList<EmailAttachment> attachments,
        CancellationToken ct)
    {
        var root = Path.Combine(env.ContentRootPath, "App_Data", "email-queue", messageId.ToString());
        Directory.CreateDirectory(root);
        var list = new List<StoredAttachmentMeta>();

        foreach (var a in attachments)
        {
            if (a.Content.Length == 0) continue;
            var safeName = SanitizeFileName(a.FileName);
            var path = Path.Combine(root, safeName);
            await File.WriteAllBytesAsync(path, a.Content, ct);
            list.Add(new StoredAttachmentMeta(
                safeName,
                string.IsNullOrWhiteSpace(a.ContentType) ? "application/octet-stream" : a.ContentType,
                Path.Combine("email-queue", messageId.ToString(), safeName).Replace('\\', '/')));
        }

        return JsonSerializer.Serialize(list, JsonOpts);
    }

    internal static string SanitizeFileName(string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "attachment.bin" : Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return n.Length == 0 ? "attachment.bin" : n;
    }

    internal sealed record StoredAttachmentMeta(string FileName, string ContentType, string RelativePath);
}
