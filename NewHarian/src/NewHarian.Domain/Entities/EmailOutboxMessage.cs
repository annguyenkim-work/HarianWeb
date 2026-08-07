using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class EmailOutboxMessage
{
    public long Id { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    /// <summary>JSON list of { fileName, contentType, relativePath } under App_Data.</summary>
    public string? AttachmentsJson { get; set; }
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
