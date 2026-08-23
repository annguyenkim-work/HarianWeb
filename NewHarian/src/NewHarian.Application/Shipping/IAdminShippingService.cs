namespace NewHarian.Application.Shipping;

public sealed record AdminShippingRow(int ProvinceId, string Code, string Name, decimal Fee, bool IsActive);

public interface IAdminShippingService
{
    Task<IReadOnlyList<AdminShippingRow>> ListAsync(CancellationToken ct = default);
    Task<bool> SaveAsync(int provinceId, decimal fee, bool isActive, CancellationToken ct = default);
}
