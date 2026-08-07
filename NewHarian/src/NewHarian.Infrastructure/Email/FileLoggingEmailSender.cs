using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;

namespace NewHarian.Infrastructure.Email;

/// <summary>
/// Dev / fallback: ghi file HTML vào App_Data/outbox + log.
/// Dùng khi Email:Smtp:Enabled=false, và luôn copy sau khi SMTP gửi thành công/thất bại.
/// </summary>
public sealed class FileLoggingEmailSender(
    ILogger<FileLoggingEmailSender> logger,
    IWebHostEnvironment env) : IEmailSender
{
    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "outbox");
        Directory.CreateDirectory(dir);

        var safeTo = string.Join("_", to.Split(Path.GetInvalidFileNameChars()));
        var safeSub = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));
        if (safeSub.Length > 40) safeSub = safeSub[..40];
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{stamp}_{safeTo}_{safeSub}.html";
        var path = Path.Combine(dir, fileName);

        var attachmentNote = "";
        if (attachments is { Count: > 0 })
        {
            var names = string.Join(", ", attachments.Select(a => a.FileName));
            attachmentNote = $"<p><strong>Attachments:</strong> {System.Net.WebUtility.HtmlEncode(names)}</p>";
            var attachDir = Path.Combine(dir, $"{stamp}_{safeTo}_attachments");
            Directory.CreateDirectory(attachDir);
            foreach (var a in attachments)
            {
                var safeName = string.Join("_", (a.FileName ?? "file").Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "attachment.bin";
                await File.WriteAllBytesAsync(Path.Combine(attachDir, safeName), a.Content, cancellationToken);
            }
        }

        var html = $"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><title>{System.Net.WebUtility.HtmlEncode(subject)}</title></head>
            <body>
            <p><strong>To:</strong> {System.Net.WebUtility.HtmlEncode(to)}</p>
            <p><strong>Subject:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>
            {attachmentNote}
            <hr/>
            {htmlBody}
            </body></html>
            """;

        await File.WriteAllTextAsync(path, html, cancellationToken);
        logger.LogInformation("Email saved → {Path} | To={To} | Subject={Subject} | Attachments={Count}",
            path, to, subject, attachments?.Count ?? 0);
    }
}
