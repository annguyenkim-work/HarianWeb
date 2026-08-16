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

    public async Task<(decimal Fee, bool IsFreeShipping)> CalculateFeeAsync(decimal subTotal, int provinceId, CancellationToken ct = default)
    {
        var threshold = await GetFreeThresholdAsync(ct);
        if (subTotal >= threshold)
            return (0m, true);

        var fee = await db.ShippingRates.AsNoTracking()
            .Where(r => r.ProvinceId == provinceId)
            .Select(r => (decimal?)r.Fee)
            .FirstOrDefaultAsync(ct) ?? 30000m;

        return (fee, false);
    }

    public async Task<(decimal Fee, bool IsFreeShipping)> CalculateFeeAsync(decimal subTotal, string provinceCode, CancellationToken ct = default)
    {
        var code = provinceCode?.Trim() ?? "";
        var id = await db.ShippingProvinces.AsNoTracking()
            .Where(p => p.IsActive && p.Code == code)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (id is null)
            return (30000m, false);
        return await CalculateFeeAsync(subTotal, id.Value, ct);
    }

    public async Task<decimal> GetFreeThresholdAsync(CancellationToken ct = default)
    {
        var raw = await db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == "shipping.free_threshold")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        return decimal.TryParse(raw, out var v) ? v : 1_000_000m;
    }
}
