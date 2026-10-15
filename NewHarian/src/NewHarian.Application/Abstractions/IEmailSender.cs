namespace NewHarian.Application.Abstractions;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default,
        IReadOnlyList<EmailAttachment>? attachments = null);
}

/// <summary>Mail whose body carries a secret: the stored copy is wiped once delivered (or given up).</summary>
public interface ISensitiveEmailSender
{
    Task SendSensitiveAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public record EmailMessage(string To, string Subject, string HtmlBody);
