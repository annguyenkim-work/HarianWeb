using NewHarian.Domain.Enums;

namespace NewHarian.Application.Catalog;

public class ServiceBookingRequest
{
    public int ServiceVariantId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CitizenId { get; set; }
    public DateOnly PreferredDate { get; set; }
    public string PreferredTime { get; set; } = string.Empty;
    public string? ServiceAddress { get; set; }
    public string? ProvinceCode { get; set; }
    public string? CommuneCode { get; set; }
    public string? Notes { get; set; }
    public string LanguageCode { get; set; } = "vi";
}

public record ServiceBookingListItemDto(
    int Id,
    string BookingNumber,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ProductName,
    string VariantLabel,
    DateOnly PreferredDate,
    string PreferredTime,
    ServiceBookingStatus Status,
    DateTime CreatedAt);

public record ServiceBookingDetailDto(
    int Id,
    string BookingNumber,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string? CitizenId,
    string ProductName,
    string VariantLabel,
    DateOnly PreferredDate,
    string PreferredTime,
    string? ServiceAddress,
    string? ProvinceCode,
    string? ProvinceName,
    string? CommuneCode,
    string? CommuneName,
    string? Notes,
    string? InternalNotes,
    decimal? Amount,
    ServiceBookingStatus Status,
    string LanguageCode,
    DateTime CreatedAt);

public interface IServiceBookingService
{
    Task<(bool Ok, string? Error, string? BookingNumber)> CreateAsync(ServiceBookingRequest request, CancellationToken ct = default);
    Task<(IReadOnlyList<ServiceBookingListItemDto> Items, int Total)> ListAsync(
        ServiceBookingStatus? status,
        string? q = null,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);
    Task<ServiceBookingDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> UpdateStatusAsync(int id, ServiceBookingStatus status, string? internalNotes, string? citizenId, decimal? amount, CancellationToken ct = default);
}
