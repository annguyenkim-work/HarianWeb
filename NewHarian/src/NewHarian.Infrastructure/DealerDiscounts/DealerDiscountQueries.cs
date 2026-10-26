using Microsoft.EntityFrameworkCore;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.DealerDiscounts;

internal static class DealerDiscountQueries
{
    public static async Task<List<DealerDiscountGroupDto>> LoadGroupsAsync(AppDbContext db, CancellationToken ct)
    {
        var groups = await db.DealerDiscountGroups.AsNoTracking()
            .Include(g => g.Variants).ThenInclude(v => v.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Translations)
            .Include(g => g.Tiers).ThenInclude(t => t.Rates)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
            .ToListAsync(ct);

        return groups.Select(g => new DealerDiscountGroupDto(
                g.Id,
                g.Name,
                g.Tiers.OrderBy(t => t.SortOrder).ThenBy(t => t.MinQuantity)
                    .Select(t => new DealerDiscountTierDto(t.Id, t.MinQuantity, t.MaxQuantity)).ToList(),
                g.Variants.Select(v => ToSkuDto(v.ProductVariant))
                    .OrderBy(s => s.Label).ToList(),
                g.Tiers.SelectMany(t => t.Rates).ToDictionary(r => (r.TierId, r.ProductVariantId), r => r.Percent)))
            .ToList();
    }

    public static async Task<Dictionary<(int TierId, int ProductVariantId), decimal>> LoadOverridesAsync(
        AppDbContext db, int dealerId, CancellationToken ct)
        => await db.DealerDiscountOverrides.AsNoTracking()
            .Where(o => o.DealerId == dealerId)
            .ToDictionaryAsync(o => (o.TierId, o.ProductVariantId), o => o.Percent, ct);

    public static DealerDiscountSkuDto ToSkuDto(ProductVariant v)
        => new(v.Id, SkuLabel(v), v.Sku);

    public static string SkuLabel(ProductVariant v)
    {
        var productName = v.Product.Translations
            .OrderBy(t => t.LanguageCode == "vi" ? 0 : 1)
            .Select(t => t.Name)
            .FirstOrDefault() ?? v.Product.Slug;
        var variant = string.IsNullOrWhiteSpace(v.VariantLabel) ? v.Sku : v.VariantLabel;
        return $"{productName} - {variant} ({v.Sku})";
    }
}
