namespace NewHarian.Application.Shipping;

public record ProvinceOptionDto(int Id, string Code, string Name);

public interface IShippingService
{
    Task<IReadOnlyList<ProvinceOptionDto>> GetActiveProvincesAsync(string lang, CancellationToken ct = default);
    Task<(decimal Fee, bool IsFreeShipping)> CalculateFeeAsync(decimal subTotal, int provinceId, CancellationToken ct = default);
    Task<(decimal Fee, bool IsFreeShipping)> CalculateFeeAsync(decimal subTotal, string provinceCode, CancellationToken ct = default);
    Task<decimal> GetFreeThresholdAsync(CancellationToken ct = default);
}
