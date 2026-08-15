using NewHarian.Domain.Enums;

namespace NewHarian.Application.Dealers;

public class DealerFormModel
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Message { get; set; }
    /// <summary>Honeypot — must stay empty.</summary>
    public string? Website { get; set; }
}

public record DealerListItemDto(
    int Id, DateTime CreatedAt, string FullName, string Phone, string Email,
    DealerStatus Status, decimal? DiscountPercent);

public record DealerDetailDto(
    int Id, DateTime CreatedAt, string FullName, string Phone, string Email, string Address,
    string? Message, DealerStatus Status, decimal? DiscountPercent, string? InternalNotes,
    string? ReviewedByUserId, string LanguageCode, DateTime? ReviewedAt);

public record DealerOptionDto(
    int Id, string FullName, string Phone, string Email, string Address, decimal DiscountPercent);

public class DealerCreateRequest
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Message { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? InternalNotes { get; set; }
}

public interface IDealerService
{
    Task<(bool Ok, string? Error, int? Id)> SubmitAsync(DealerFormModel model, string lang, CancellationToken ct = default);
    Task<IReadOnlyList<DealerListItemDto>> ListAsync(DealerStatus? status, string? q = null, string? sort = null, string? dir = null, CancellationToken ct = default);
    Task<DealerDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ApproveAsync(int id, decimal discountPercent, string? notes, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> RejectAsync(int id, string? notes, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SaveApprovedAsync(int id, decimal discountPercent, string? notes, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> CreateApprovedAsync(DealerCreateRequest request, string? userId, CancellationToken ct = default);
    Task<IReadOnlyList<DealerOptionDto>> ListApprovedOptionsAsync(CancellationToken ct = default);
}
