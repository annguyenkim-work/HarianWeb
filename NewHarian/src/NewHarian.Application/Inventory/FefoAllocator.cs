using NewHarian.Domain.Entities;

namespace NewHarian.Application.Inventory;

public sealed record FefoAllocation(StockLot Lot, int Quantity);

/// <param name="ExpiredOnHand">Quantity sitting in expired lots that were skipped (not shippable).</param>
public sealed record FefoPlan(int Requested, IReadOnlyList<FefoAllocation> Allocations, int ExpiredOnHand = 0)
{
    public int Allocated => Allocations.Sum(a => a.Quantity);
    public int Shortfall => Requested - Allocated;
}

/// <summary>
/// First-expired-first-out pick: earliest ExpiryDate, then earliest ReceivedAt.
/// Expired lots (<see cref="StockExpiry.IsExpired"/>) are never picked; their stock counts as shortfall.
/// Lots with QuantityOnHand ≤ 0 are skipped (tracked entities may already be drained by an earlier line).
/// </summary>
public static class FefoAllocator
{
    public static FefoPlan Plan(IEnumerable<StockLot> lots, int requestedQty, DateOnly today)
    {
        var allocations = new List<FefoAllocation>();
        var remaining = requestedQty;
        var expiredOnHand = 0;
        foreach (var lot in lots.OrderBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt))
        {
            if (lot.QuantityOnHand <= 0) continue;
            if (StockExpiry.IsExpired(lot.ExpiryDate, today))
            {
                expiredOnHand += lot.QuantityOnHand;
                continue;
            }
            if (remaining <= 0) break;
            var take = Math.Min(lot.QuantityOnHand, remaining);
            allocations.Add(new FefoAllocation(lot, take));
            remaining -= take;
        }

        return new FefoPlan(requestedQty, allocations, expiredOnHand);
    }
}
