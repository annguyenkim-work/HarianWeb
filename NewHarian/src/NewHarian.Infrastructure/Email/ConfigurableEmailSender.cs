using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;

namespace NewHarian.Infrastructure.Email;

/// <summary>
/// Sends via SMTP when Email:Smtp:Enabled=true (retry 3×); otherwise file outbox only.
/// Always writes a copy to App_Data/outbox for audit.
/// </summary>
public sealed class ConfigurableEmailSender(
    IConfiguration config,
    ILogger<ConfigurableEmailSender> logger,
    FileLoggingEmailSender fileFallback) : IEmailSender
{
    private const int MaxAttempts = 3;

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Email recipient is required.", nameof(to));

        var enabled = config.GetValue("Email:Smtp:Enabled", false);
        if (!enabled)
        {
            logger.LogInformation("SMTP disabled — outbox only. To={To} Subject={Subject}", to, subject);
            await fileFallback.SendAsync(to, subject, htmlBody, cancellationToken, attachments);
            return;
        }

        Exception? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await SendSmtpAsync(to, subject, htmlBody, attachments, cancellationToken);
                logger.LogInformation("SMTP email sent To={To} Subject={Subject} Attempt={Attempt} Attachments={Count}",
                    to, subject, attempt, attachments?.Count ?? 0);
                await fileFallback.SendAsync(to, subject, htmlBody, cancellationToken, attachments);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                last = ex;
                var delayMs = (int)Math.Pow(2, attempt - 1) * 500; // 500ms, 1s
                logger.LogWarning(ex, "SMTP send failed To={To} Attempt={Attempt}/{Max} — retry in {Delay}ms",
                    to, attempt, MaxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        // Keep a copy even when SMTP fails (ops can still open outbox)
        try { await fileFallback.SendAsync(to, subject, htmlBody, cancellationToken, attachments); }
        catch (Exception outboxEx)
        {
            logger.LogError(outboxEx, "Failed writing email outbox after SMTP error To={To}", to);
        }

        logger.LogError(last, "SMTP email failed after {Max} attempts To={To} Subject={Subject}", MaxAttempts, to, subject);
        throw new InvalidOperationException($"Không gửi được email tới {to} sau {MaxAttempts} lần thử.", last);
    }

    private async Task SendSmtpAsync(
        string to,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments,
        CancellationToken cancellationToken)
    {
        var host = config["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Email:Smtp:Host chưa cấu hình.");

        var port = config.GetValue("Email:Smtp:Port", 587);
        var user = config["Email:Smtp:User"];
        var pass = config["Email:Smtp:Password"];
        var fromAddress = config["Email:Smtp:From"] ?? user
            ?? throw new InvalidOperationException("Email:Smtp:From hoặc User chưa cấu hình.");
        var fromName = config["Email:Smtp:FromName"] ?? "Harian";
        var useSsl = config.GetValue("Email:Smtp:UseSsl", true);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30_000
        };
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, pass);

        using var msg = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(to);

        var streams = new List<MemoryStream>();
        try
        {
            if (attachments is { Count: > 0 })
            {
                foreach (var a in attachments)
                {
                    if (a.Content.Length == 0) continue;
                    var ms = new MemoryStream(a.Content);
                    streams.Add(ms);
                    var name = string.IsNullOrWhiteSpace(a.FileName) ? "attachment.bin" : a.FileName;
                    var contentType = string.IsNullOrWhiteSpace(a.ContentType)
                        ? "application/octet-stream"
                        : a.ContentType;
                    msg.Attachments.Add(new Attachment(ms, name, contentType));
                }
            }

            await client.SendMailAsync(msg, cancellationToken);
        }
        finally
        {
            foreach (var s in streams)
                await s.DisposeAsync();
        }
    }
}
