using NewHarian.Application.Shipping;

namespace NewHarian.UnitTests.Application.Shipping;

[Trait("Category", "Unit")]
public class ShippingFeeCopyTests
{
    [Fact]
    public void Email_note_for_a_new_order_says_staff_will_quote()
        => Assert.Equal(ShippingFeeCopy.PendingVi, ShippingFeeCopy.EmailNote(0));

    [Fact]
    public void Email_note_for_a_stored_fee_includes_the_amount()
        => Assert.Equal($"Phí vận chuyển: {45_000m.ToString("N0")} VNĐ.", ShippingFeeCopy.EmailNote(45_000m));

    [Fact]
    public void Legacy_email_sentence_is_rewritten_once()
    {
        const string legacy = "<p>Tạm tính {{SubTotal}} VNĐ + Ship {{ShippingFee}} VNĐ = <strong>{{Total}} VNĐ</strong></p>";
        var once = ShippingFeeCopy.ReplaceLegacyEmailShipLine(legacy);
        Assert.Equal(
            "<p>Tạm tính {{SubTotal}} VNĐ. {{ShippingNote}} Tổng tiền hàng: <strong>{{Total}} VNĐ</strong></p>",
            once);
        Assert.Equal(once, ShippingFeeCopy.ReplaceLegacyEmailShipLine(once));
    }

    [Theory]
    [InlineData(
        "phí vận chuyển tính theo tỉnh/thành tại checkout.",
        "phí vận chuyển do nhân viên thông báo khi xác nhận đơn, không tính trên website.")]
    [InlineData(
        "shipping is calculated by province at checkout.",
        "shipping is quoted by staff when the order is confirmed and is not calculated on the website.")]
    [InlineData(
        "送料はチェックアウト時に都道府県で計算します。",
        "送料は注文確認時にスタッフから案内し、サイト上では計算しません。")]
    public void Legacy_terms_sentence_is_rewritten(string oldText, string newText)
    {
        var updated = ShippingFeeCopy.ReplaceLegacyTerms("<p>" + oldText + "</p>");
        Assert.Equal("<p>" + newText + "</p>", updated);
        Assert.Equal(updated, ShippingFeeCopy.ReplaceLegacyTerms(updated));
    }
}
