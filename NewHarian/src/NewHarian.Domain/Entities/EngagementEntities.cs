using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class MediaFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? AltText { get; set; }
    public string? UploadedByUserId { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Inquiry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public InquiryStatus Status { get; set; } = InquiryStatus.New;
    public string? InternalNotes { get; set; }
    public string? HandledByUserId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public class JobApplication
{
    public int Id { get; set; }
    public int? SitePostId { get; set; }
    public ApplicationType ApplicationType { get; set; }
    public string? Gender { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Prefecture { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? AttachmentMediaFileId { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.New;
    public string? InternalNotes { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public SitePost? SitePost { get; set; }
    public MediaFile? Attachment { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
