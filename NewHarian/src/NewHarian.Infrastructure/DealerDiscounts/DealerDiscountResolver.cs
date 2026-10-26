using NewHarian.Application.DealerDiscounts;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.DealerDiscounts;

public sealed class DealerDiscountResolver(AppDbContext db) : IDealerDiscountResolver
{
    public async Task<DealerDiscountPolicy> GetPolicyAsync(int dealerId, CancellationToken ct = default)
    {
        var groups = await DealerDiscountQueries.LoadGroupsAsync(db, ct);
        var overrides = await DealerDiscountQueries.LoadOverridesAsync(db, dealerId, ct);
        var configs = groups.Select(g => new DiscountGroupConfig(
                g.Id,
                g.Name,
                g.Skus.Select(s => s.ProductVariantId).ToList(),
                g.Tiers.Select(t => new DiscountTierConfig(
                        t.Id, t.MinQuantity, t.MaxQuantity,
                        g.Rates.Where(r => r.Key.TierId == t.Id).ToDictionary(r => r.Key.ProductVariantId, r => r.Value)))
                    .ToList()))
            .ToList();
        return new DealerDiscountPolicy(configs, overrides);
    }
}
