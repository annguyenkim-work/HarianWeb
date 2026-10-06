using System.Globalization;
using NewHarian.Application.Inventory;
using NewHarian.Domain.Entities;

namespace NewHarian.UnitTests.Application.Inventory;

[Trait("Category", "Unit")]
public class FefoAllocatorTests
{
    private static readonly DateTime Received = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 1, 1);

    private static StockLot Lot(int id, string expiry, int qty, int receivedDayOffset = 0) => new()
    {
        Id = id,
        ExpiryDate = DateOnly.Parse(expiry, CultureInfo.InvariantCulture),
        QuantityOnHand = qty,
        ReceivedAt = Received.AddDays(receivedDayOffset)
    };

    private static void AssertPicks(FefoPlan plan, params (int LotId, int Qty)[] expected)
        => Assert.Equal(expected, plan.Allocations.Select(a => (a.Lot.Id, a.Quantity)).ToArray());

    [Fact]
    public void Exact_fit_takes_whole_lot()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2026-12-31", 5)], 5, Today);

        AssertPicks(plan, (1, 5));
        Assert.Equal(5, plan.Allocated);
        Assert.Equal(0, plan.Shortfall);
    }

    [Fact]
    public void Partial_take_from_single_lot()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2026-12-31", 10)], 3, Today);

        AssertPicks(plan, (1, 3));
        Assert.Equal(0, plan.Shortfall);
    }

    [Fact]
    public void Spans_lots_earliest_expiry_first_regardless_of_input_order()
    {
        var lots = new[]
        {
            Lot(1, "2027-06-30", 10),
            Lot(2, "2026-11-30", 2),
            Lot(3, "2027-01-31", 3),
        };

        var plan = FefoAllocator.Plan(lots, 7, Today);

        AssertPicks(plan, (2, 2), (3, 3), (1, 2));
        Assert.Equal(7, plan.Allocated);
        Assert.Equal(0, plan.Shortfall);
    }

    [Fact]
    public void Same_expiry_prefers_earliest_received()
    {
        var lots = new[]
        {
            Lot(1, "2026-12-31", 4, receivedDayOffset: 5),
            Lot(2, "2026-12-31", 4, receivedDayOffset: 1),
        };

        var plan = FefoAllocator.Plan(lots, 6, Today);

        AssertPicks(plan, (2, 4), (1, 2));
    }

    [Fact]
    public void Full_tie_keeps_input_order()
    {
        var lots = new[] { Lot(7, "2026-12-31", 2), Lot(3, "2026-12-31", 2) };

        var plan = FefoAllocator.Plan(lots, 3, Today);

        AssertPicks(plan, (7, 2), (3, 1));
    }

    [Fact]
    public void Reports_shortfall_when_stock_is_insufficient()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2026-12-31", 2), Lot(2, "2027-12-31", 3)], 9, Today);

        AssertPicks(plan, (1, 2), (2, 3));
        Assert.Equal(5, plan.Allocated);
        Assert.Equal(4, plan.Shortfall);
    }

    [Fact]
    public void No_lots_means_everything_is_short()
    {
        var plan = FefoAllocator.Plan([], 4, Today);

        Assert.Empty(plan.Allocations);
        Assert.Equal(0, plan.Allocated);
        Assert.Equal(4, plan.Shortfall);
    }

    [Fact]
    public void Skips_lots_already_drained_to_zero_or_below()
    {
        var lots = new[]
        {
            Lot(1, "2026-10-31", 0),
            Lot(2, "2026-11-30", -2),
            Lot(3, "2026-12-31", 5),
        };

        var plan = FefoAllocator.Plan(lots, 3, Today);

        AssertPicks(plan, (3, 3));
    }

    [Fact]
    public void Expired_lots_are_skipped()
    {
        var lots = new[] { Lot(1, "2027-12-31", 5), Lot(2, "2020-01-01", 5) };

        var plan = FefoAllocator.Plan(lots, 1, Today);

        AssertPicks(plan, (1, 1));
        Assert.Equal(0, plan.Shortfall);
        Assert.Equal(5, plan.ExpiredOnHand);
    }

    [Fact]
    public void Lot_expiring_today_is_still_pickable()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2026-01-01", 3)], 2, Today);

        AssertPicks(plan, (1, 2));
        Assert.Equal(0, plan.ExpiredOnHand);
    }

    [Fact]
    public void Lot_expired_yesterday_is_skipped()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2025-12-31", 3)], 2, Today);

        Assert.Empty(plan.Allocations);
        Assert.Equal(2, plan.Shortfall);
        Assert.Equal(3, plan.ExpiredOnHand);
    }

    [Fact]
    public void Only_expired_stock_means_full_shortfall()
    {
        var lots = new[] { Lot(1, "2025-06-30", 4), Lot(2, "2025-12-01", 6) };

        var plan = FefoAllocator.Plan(lots, 5, Today);

        Assert.Empty(plan.Allocations);
        Assert.Equal(0, plan.Allocated);
        Assert.Equal(5, plan.Shortfall);
        Assert.Equal(10, plan.ExpiredOnHand);
    }

    [Fact]
    public void Mix_of_expired_and_valid_takes_valid_only_and_reports_shortfall()
    {
        var lots = new[]
        {
            Lot(1, "2025-11-30", 10),
            Lot(2, "2026-03-31", 2),
            Lot(3, "2026-06-30", 1),
        };

        var plan = FefoAllocator.Plan(lots, 5, Today);

        AssertPicks(plan, (2, 2), (3, 1));
        Assert.Equal(2, plan.Shortfall);
        Assert.Equal(10, plan.ExpiredOnHand);
    }

    [Fact]
    public void Drained_expired_lots_do_not_count_as_expired_on_hand()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2025-01-01", 0), Lot(2, "2025-02-01", -3)], 1, Today);

        Assert.Equal(0, plan.ExpiredOnHand);
        Assert.Equal(1, plan.Shortfall);
    }

    [Fact]
    public void Stops_once_request_is_satisfied()
    {
        var lots = new[] { Lot(1, "2026-10-31", 5), Lot(2, "2026-11-30", 5), Lot(3, "2026-12-31", 5) };

        var plan = FefoAllocator.Plan(lots, 5, Today);

        AssertPicks(plan, (1, 5));
    }

    [Fact]
    public void Zero_request_allocates_nothing()
    {
        var plan = FefoAllocator.Plan([Lot(1, "2026-12-31", 5)], 0, Today);

        Assert.Empty(plan.Allocations);
        Assert.Equal(0, plan.Shortfall);
    }

    [Fact]
    public void Does_not_mutate_lots()
    {
        var lot = Lot(1, "2026-12-31", 5);

        FefoAllocator.Plan([lot], 3, Today);

        Assert.Equal(5, lot.QuantityOnHand);
    }
}
