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

public record EmailMessage(string To, string Subject, string HtmlBody);
