using NewHarian.Application.Formatting;
using NewHarian.Application.PromoCodes;

namespace NewHarian.UnitTests.Application.PromoCodes;

[Trait("Category", "Unit")]
public class PromoCodeRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);

    private static PromoCodeState Code(
        decimal percent = 10,
        int maxUses = 5,
        int used = 0,
        bool active = true,
        DateTime? from = null,
        DateTime? to = null,
        string code = "SALE10")
        => new(1, code, percent, from ?? Now.AddDays(-1), to ?? Now.AddDays(1), maxUses, used, active);

    [Theory]
    [InlineData(" sale10 ", "SALE10")]
    [InlineData("Sale-10", "SALE-10")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Normalize_strips_space_and_uppercases(string? input, string? expected)
        => Assert.Equal(expected, PromoCodeRules.Normalize(input));

    [Theory]
    [InlineData("AB", false)]
    [InlineData("ABC", true)]
    [InlineData("SALE-10", true)]
    [InlineData("SALE 10", false)]
    [InlineData("GIẢM10", false)]
    public void Code_shape_is_ascii_letters_digits_or_hyphen(string code, bool ok)
        => Assert.Equal(ok, PromoCodeRules.IsCodeShape(code));

    [Fact]
    public void Blank_code_is_not_a_discount()
    {
        var quote = PromoCodeRules.Evaluate(null, null, Now, 100_000, isDealer: false);
        Assert.True(quote.Ok);
        Assert.False(quote.HasDiscount);
    }

    [Fact]
    public void Dealer_session_cannot_use_a_code()
    {
        var quote = PromoCodeRules.Evaluate("SALE10", Code(), Now, 100_000, isDealer: true);
        Assert.Equal(PromoCodeRules.RetailOnly, quote.Error);
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("NO")]
    public void Unknown_or_malformed_code_is_rejected(string code)
    {
        var quote = PromoCodeRules.Evaluate(code, code == "UNKNOWN" ? null : Code(), Now, 100_000, false);
        Assert.Equal(PromoCodeRules.Invalid, quote.Error);
    }

    [Fact]
    public void Inactive_code_is_rejected_before_the_window()
    {
        var quote = PromoCodeRules.Evaluate("SALE10", Code(active: false, from: Now.AddDays(2)), Now, 100_000, false);
        Assert.Equal(PromoCodeRules.Inactive, quote.Error);
    }

    [Theory]
    [InlineData(-60, PromoCodeRules.NotStarted)]
    [InlineData(60, PromoCodeRules.Expired)]
    public void Window_is_inclusive_at_the_stored_instant(int offsetMinutes, string? errorWhenOutside)
    {
        var from = Now.AddMinutes(-10);
        var to = Now.AddMinutes(10);
        var atEdge = PromoCodeRules.Evaluate("SALE10", Code(from: from, to: to), offsetMinutes < 0 ? from : to, 100_000, false);
        Assert.True(atEdge.HasDiscount);

        var outside = Now.AddMinutes(offsetMinutes);
        var state = offsetMinutes < 0 ? Code(from: Now, to: Now.AddHours(1)) : Code(from: Now.AddHours(-2), to: Now.AddMinutes(-1));
        var quote = PromoCodeRules.Evaluate("SALE10", state, outside, 100_000, false);
        Assert.Equal(errorWhenOutside, quote.Error);
    }

    [Fact]
    public void Last_remaining_use_applies_and_a_spent_code_does_not()
    {
        Assert.True(PromoCodeRules.Evaluate("SALE10", Code(maxUses: 1, used: 0), Now, 100_000, false).HasDiscount);
        Assert.Equal(PromoCodeRules.Exhausted, PromoCodeRules.Evaluate("SALE10", Code(maxUses: 1, used: 1), Now, 100_000, false).Error);
    }

    [Theory]
    [InlineData(200_000, 10, 20_000)]
    [InlineData(100_000, 100, 100_000)]
    [InlineData(99_999, 33.33, 33_330)]
    public void Percent_rounds_to_the_nearest_dong(decimal goods, decimal percent, decimal expected)
    {
        var quote = PromoCodeRules.Evaluate("SALE10", Code(percent: percent), Now, goods, false);
        Assert.Equal(expected, quote.Amount);
    }

    [Fact]
    public void Tiny_percent_on_a_small_subtotal_does_not_burn_a_use_for_zero_dong()
    {
        var quote = PromoCodeRules.Evaluate("SALE10", Code(percent: 0.01m), Now, 100, false);
        Assert.Equal(PromoCodeRules.TooSmall, quote.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void No_goods_value_cannot_take_a_code(decimal goods)
        => Assert.Equal(PromoCodeRules.NoGoods, PromoCodeRules.Evaluate("SALE10", Code(), Now, goods, false).Error);

    [Theory]
    [InlineData(0, PromoCodeRules.PercentInvalid)]
    [InlineData(100.01, PromoCodeRules.PercentInvalid)]
    [InlineData(10.555, PromoCodeRules.PercentScale)]
    public void Percent_must_be_between_0_01_and_100_with_two_decimals(decimal percent, string error)
        => Assert.Equal(error, PromoCodeRules.ValidateDefinition("SALE10", percent, Now, Now.AddDays(1), 1, 0));

    [Fact]
    public void End_must_be_after_start_and_quantity_cannot_drop_below_uses()
    {
        Assert.Equal(PromoCodeRules.DateOrder, PromoCodeRules.ValidateDefinition("SALE10", 10, Now, Now, 1, 0));
        Assert.Equal(PromoCodeRules.QuantityInvalid, PromoCodeRules.ValidateDefinition("SALE10", 10, Now, Now.AddHours(1), 0, 0));
        Assert.Equal(PromoCodeRules.QuantityBelowUsed, PromoCodeRules.ValidateDefinition("SALE10", 10, Now, Now.AddHours(1), 2, 3));
        Assert.Null(PromoCodeRules.ValidateDefinition("SALE10", 10, Now, Now.AddHours(1), 3, 3));
    }

    [Theory]
    [InlineData(false, 0, 1, "Tắt")]
    [InlineData(true, 1, 1, "Hết lượt")]
    public void Status_label_prefers_off_then_spent(bool active, int used, int max, string label)
    {
        var state = Code(active: active, maxUses: max, used: used);
        Assert.Equal(label, PromoCodeRules.StatusLabel(state, Now));
    }

    [Fact]
    public void Vietnam_time_is_seven_hours_ahead_of_utc()
    {
        var utc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 7, 7, 0, 0), VietnamTime.ToLocal(utc));
        Assert.Equal(utc, VietnamTime.ToUtc(new DateTime(2026, 11, 7, 7, 0, 0)));
        Assert.Equal("07/11/2026 07:00", VietnamTime.Format(utc));
    }
}
