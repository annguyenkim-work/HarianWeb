using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Shipping;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Shipping;

public class ShippingService(AppDbContext db) : IShippingService
{
    public async Task<IReadOnlyList<ProvinceOptionDto>> GetActiveProvincesAsync(string lang, CancellationToken ct = default)
    {
        var list = await db.ShippingProvinces.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return list.Select(p => new ProvinceOptionDto(
            p.Id,
            p.Code,
            lang switch
            {
                "en" => p.NameEn ?? p.NameVi,
                "ja" => p.NameJa ?? p.NameVi,
                _ => p.NameVi
            })).ToList();
    }

    public async Task<decimal> CalculateFeeAsync(int provinceId, CancellationToken ct = default)
    {
        var fee = await db.ShippingRates.AsNoTracking()
            .Where(r => r.ProvinceId == provinceId)
            .Select(r => (decimal?)r.Fee)
            .FirstOrDefaultAsync(ct);
        return fee ?? ShippingFees.DefaultVnd;
    }

    public async Task<decimal> CalculateFeeAsync(string provinceCode, CancellationToken ct = default)
    {
        var code = provinceCode?.Trim() ?? "";
        var id = await db.ShippingProvinces.AsNoTracking()
            .Where(p => p.IsActive && p.Code == code)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (id is null)
            return ShippingFees.DefaultVnd;
        return await CalculateFeeAsync(id.Value, ct);
    }
}
