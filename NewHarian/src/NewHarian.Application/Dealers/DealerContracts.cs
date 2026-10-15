using NewHarian.Domain.Enums;

namespace NewHarian.Application.Dealers;

/// <summary>Dealer profile fields shared by the guest form and the admin modal.</summary>
public class DealerProfileInput
{
    public DealerBusinessType BusinessType { get; set; } = DealerBusinessType.Company;
    public string BusinessName { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string? HouseholdRegistrationNo { get; set; }
    public string RepresentativeName { get; set; } = "";
    public string? RepresentativeTitle { get; set; }
    public string? CitizenId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string ProvinceCode { get; set; } = "";
    public string CommuneCode { get; set; } = "";
    public string Address { get; set; } = "";
    public bool ShipToHeadOffice { get; set; } = true;
    public string? ShippingProvinceCode { get; set; }
    public string? ShippingCommuneCode { get; set; }
    public string? ShippingAddress { get; set; }
    public string? InvoiceEmail { get; set; }
    public string? Message { get; set; }
}

public class DealerFormModel : DealerProfileInput
{
    /// <summary>Honeypot: must stay empty.</summary>
    public string? Website { get; set; }
    public int? BusinessLicenseMediaFileId { get; set; }
    public string? BusinessLicenseFileName { get; set; }
}

public class DealerEditRequest : DealerProfileInput
{
    public int? Id { get; set; }
    public string? InternalNotes { get; set; }
    public int? BusinessLicenseMediaFileId { get; set; }
    public string? BusinessLicenseFileName { get; set; }
    public bool RemoveBusinessLicense { get; set; }
}

public record DealerListItemDto(
    int Id, DateTime CreatedAt, DealerBusinessType BusinessType, string BusinessName, string TaxCode,
    string FullName, string Phone, string Email, DealerStatus Status, bool HasDiscountOverrides);

public class DealerDetailDto
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DealerBusinessType BusinessType { get; init; }
    public string BusinessName { get; init; } = "";
    public string TaxCode { get; init; } = "";
    public string? HouseholdRegistrationNo { get; init; }
    public string RepresentativeName { get; init; } = "";
    public string? RepresentativeTitle { get; init; }
    public string? CitizenId { get; init; }
    public string FullName { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string HeadOfficeAddress { get; init; } = "";
    public bool ShipToHeadOffice { get; init; }
    public string? ShippingAddress { get; init; }
    public string? InvoiceEmail { get; init; }
    public int? BusinessLicenseMediaFileId { get; init; }
    public string? BusinessLicenseFileName { get; init; }
    public string? Message { get; init; }
    public DealerStatus Status { get; init; }
    public string? InternalNotes { get; init; }
    public string? ReviewedByUserId { get; init; }
    public string LanguageCode { get; init; } = "vi";
    public DateTime? ReviewedAt { get; init; }
    public string? AccessCodeLast4 { get; init; }
    public DateTime? AccessCodeIssuedAt { get; init; }
    public string? AccessCodeIssuedByUserId { get; init; }
}

/// <summary>Approved dealer as offered on the manual order form (prefill + invoice preview).</summary>
public class DealerOptionDto
{
    public int Id { get; init; }
    public DealerBusinessType BusinessType { get; init; }
    public string BusinessName { get; init; } = "";
    public string TaxCode { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string? CitizenId { get; init; }
    public string ShipAddress { get; init; } = "";
    public string? ShipProvinceCode { get; init; }
    public string? ShipCommuneCode { get; init; }
    public string InvoiceAddress { get; init; } = "";
    public string InvoiceEmail { get; init; } = "";
}

public interface IDealerRegistrationService
{
    /// <summary>Normalizes the model in place; returns the first error (incl. duplicate MST) or null.</summary>
    Task<string?> ValidateAsync(DealerFormModel model, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SubmitAsync(DealerFormModel model, string lang, CancellationToken ct = default);
}

public interface IAdminDealerService
{
    Task<IReadOnlyList<DealerListItemDto>> ListAsync(
        DealerStatus? status, DealerBusinessType? type = null, string? q = null, string? sort = null, string? dir = null,
        CancellationToken ct = default);
    Task<DealerDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<DealerEditRequest?> GetEditAsync(int id, CancellationToken ct = default);
    /// <param name="Warning">Set when a new dealer was created but the access-code email could not be queued.</param>
    Task<(bool Ok, string? Error, int? Id, string? Warning)> SaveAsync(DealerEditRequest request, string? userId, CancellationToken ct = default);
    /// <param name="Warning">Approved, but the access-code email could not be queued.</param>
    Task<(bool Ok, string? Error, string? Warning)> ApproveAsync(int id, string? notes, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> RejectAsync(int id, string? notes, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SaveNotesAsync(int id, string? notes, string? userId, CancellationToken ct = default);
    Task<int?> GetBusinessLicenseMediaIdAsync(int id, CancellationToken ct = default);
}

public interface IDealerLookup
{
    Task<IReadOnlyList<DealerOptionDto>> ListApprovedOptionsAsync(CancellationToken ct = default);
}
