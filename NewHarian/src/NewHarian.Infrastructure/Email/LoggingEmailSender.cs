using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;

namespace NewHarian.Infrastructure.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        logger.LogInformation(
            "Email stub → To={To}, Subject={Subject}, Length={Length}, Attachments={Count}",
            to, subject, htmlBody.Length, attachments?.Count ?? 0);
        return Task.CompletedTask;
    }
}
