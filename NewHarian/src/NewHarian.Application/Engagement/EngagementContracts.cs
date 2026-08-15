using NewHarian.Domain.Enums;

namespace NewHarian.Application.Engagement;

public class ContactFormModel
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Message { get; set; } = "";
    /// <summary>Honeypot — must stay empty.</summary>
    public string? Website { get; set; }
}

public record InquiryListItemDto(
    int Id, DateTime CreatedAt, string Name, string Email, string? Phone,
    InquiryStatus Status, string? HandledByUserId);

public record InquiryDetailDto(
    int Id, DateTime CreatedAt, string Name, string Email, string? Phone, string? Address,
    string? Subject, string Message, InquiryStatus Status, string? InternalNotes,
    string? HandledByUserId, string LanguageCode, DateTime? ResolvedAt);

public interface IInquiryService
{
    Task<(bool Ok, string? Error, int? Id)> SubmitAsync(ContactFormModel model, string lang, CancellationToken ct = default);
    Task<(IReadOnlyList<InquiryListItemDto> Items, int Total)> ListAsync(
        InquiryStatus? status,
        string? q = null,
        string? sort = null,
        string? dir = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);
    Task<InquiryDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> UpdateStatusAsync(int id, InquiryStatus status, string? notes, string? userId, CancellationToken ct = default);
}

public class CareerFormModel
{
    public int? SitePostId { get; set; }
    public string? JobSlug { get; set; }
    public string? JobTitle { get; set; }
    public ApplicationType ApplicationType { get; set; } = ApplicationType.Application;
    public string? Gender { get; set; }
    public string FullName { get; set; } = "";
    public int? Age { get; set; }
    public string? Prefecture { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Website { get; set; }
    public int? AttachmentMediaFileId { get; set; }
}

public record ApplicationListItemDto(
    int Id, DateTime CreatedAt, string FullName, string Email,
    ApplicationType ApplicationType, ApplicationStatus Status, bool HasCv,
    int? SitePostId, string? JobTitle);

public record ApplicationDetailDto(
    int Id, DateTime CreatedAt, ApplicationType ApplicationType, string? Gender,
    string FullName, int? Age, string? Prefecture,
    string? City, string? Address, string? Phone, string Email, string Message,
    ApplicationStatus Status, string? InternalNotes, string? ReviewedByUserId,
    string LanguageCode, DateTime? ReviewedAt, string? AttachmentUrl, string? AttachmentName,
    int? SitePostId, string? JobTitle, string? JobSlug);

public interface IJobApplicationService
{
    Task<(bool Ok, string? Error, int? Id)> SubmitAsync(CareerFormModel model, string lang, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationListItemDto>> ListAsync(
        ApplicationStatus? status,
        int? sitePostId,
        string? q = null,
        string? sort = null,
        string? dir = null,
        CancellationToken ct = default);
    Task<ApplicationDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> UpdateStatusAsync(int id, ApplicationStatus status, string? notes, string? userId, CancellationToken ct = default);
}
