namespace NewHarian.Application.Shipping;

public record ProvinceOptionDto(int Id, string Code, string Name);

public interface IShippingService
{
    Task<IReadOnlyList<ProvinceOptionDto>> GetActiveProvincesAsync(string lang, CancellationToken ct = default);
    Task<decimal> CalculateFeeAsync(int provinceId, CancellationToken ct = default);
    Task<decimal> CalculateFeeAsync(string provinceCode, CancellationToken ct = default);
}
