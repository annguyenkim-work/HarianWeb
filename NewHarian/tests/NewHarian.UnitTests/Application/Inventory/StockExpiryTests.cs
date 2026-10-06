using NewHarian.Application.Inventory;

namespace NewHarian.UnitTests.Application.Inventory;

[Trait("Category", "Unit")]
public class StockExpiryTests
{
    [Theory]
    [InlineData("2026-01-01", "2026-01-02", true)]
    [InlineData("2026-01-02", "2026-01-02", false)]
    [InlineData("2026-01-03", "2026-01-02", false)]
    public void IsExpired_means_expiry_before_today(string expiry, string today, bool expected)
        => Assert.Equal(expected, StockExpiry.IsExpired(DateOnly.Parse(expiry), DateOnly.Parse(today)));

    [Fact]
    public void Today_uses_utc_calendar_date()
        => Assert.Equal(new DateOnly(2026, 10, 6), StockExpiry.Today(new DateTime(2026, 10, 6, 23, 59, 0, DateTimeKind.Utc)));
}
