using NewHarian.Domain.Enums;

namespace NewHarian.Application.DealerDiscounts;

public sealed record DiscountTierConfig(
    int TierId, int MinQuantity, int? MaxQuantity, IReadOnlyDictionary<int, decimal> DefaultPercentByProduct);

public sealed record DiscountGroupConfig(
    int GroupId, string Name, IReadOnlyCollection<int> ProductIds, IReadOnlyList<DiscountTierConfig> Tiers);

/// <summary>Discount table for one dealer: shared groups/tiers + that dealer's per-cell overrides.</summary>
public sealed record DealerDiscountPolicy(
    IReadOnlyList<DiscountGroupConfig> Groups,
    IReadOnlyDictionary<(int TierId, int ProductId), decimal> Overrides)
{
    public static DealerDiscountPolicy Empty { get; } =
        new([], new Dictionary<(int, int), decimal>());
}

public sealed record DiscountLineInput(int ProductId, int Quantity, decimal LineTotal, decimal? ManualPercent = null);

public sealed record DiscountLineResult(
    decimal Percent,
    decimal Amount,
    string? TierLabel,
    DealerDiscountSource Source,
    bool Unconfigured,
    decimal PolicyPercent);

public sealed record DiscountResult(IReadOnlyList<DiscountLineResult> Lines, decimal TotalDiscount);

public static class DealerDiscountCalculator
{
    public static DiscountResult Calculate(DealerDiscountPolicy policy, IReadOnlyList<DiscountLineInput> lines)
    {
        var groupByProduct = new Dictionary<int, DiscountGroupConfig>();
        foreach (var g in policy.Groups)
            foreach (var pid in g.ProductIds)
                groupByProduct.TryAdd(pid, g);

        var qtyByGroup = new Dictionary<int, int>();
        foreach (var line in lines)
        {
            if (groupByProduct.TryGetValue(line.ProductId, out var g))
                qtyByGroup[g.GroupId] = qtyByGroup.GetValueOrDefault(g.GroupId) + line.Quantity;
        }

        var results = new List<DiscountLineResult>(lines.Count);
        foreach (var line in lines)
        {
            decimal policyPercent = 0;
            string? label = null;
            var source = DealerDiscountSource.None;
            var unconfigured = !groupByProduct.TryGetValue(line.ProductId, out var group);

            if (group is not null)
            {
                var tier = FindTier(group, qtyByGroup.GetValueOrDefault(group.GroupId));
                if (tier is not null)
                {
                    label = TierLabel(group.Name, tier.MinQuantity, tier.MaxQuantity);
                    if (policy.Overrides.TryGetValue((tier.TierId, line.ProductId), out var own))
                    {
                        policyPercent = own;
                        source = DealerDiscountSource.DealerOverride;
                    }
                    else if (tier.DefaultPercentByProduct.TryGetValue(line.ProductId, out var def))
                    {
                        policyPercent = def;
                        source = DealerDiscountSource.Default;
                    }
                }
            }

            var percent = policyPercent;
            if (line.ManualPercent is decimal manual)
            {
                percent = manual;
                source = DealerDiscountSource.Manual;
            }

            results.Add(new DiscountLineResult(
                percent, AmountOf(line.LineTotal, percent), label, source, unconfigured, policyPercent));
        }

        return new DiscountResult(results, results.Sum(r => r.Amount));
    }

    public static decimal AmountOf(decimal lineTotal, decimal percent)
        => Math.Round(lineTotal * percent / 100m, 0, MidpointRounding.AwayFromZero);

    public static string TierRange(int min, int? max) => max is null ? $"Từ {min}" : $"{min}-{max}";

    public static string TierLabel(string groupName, int min, int? max) => $"{groupName}: {TierRange(min, max)}";

    private static DiscountTierConfig? FindTier(DiscountGroupConfig group, int qty)
        => group.Tiers.FirstOrDefault(t => qty >= t.MinQuantity && (t.MaxQuantity is null || qty <= t.MaxQuantity));
}
