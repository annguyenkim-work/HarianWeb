namespace NewHarian.Application.Shipping;

/// <summary>
/// Guest copy for a fee the website no longer calculates.
/// A stored fee (older orders) is still shown; a zero fee means staff will quote it.
/// </summary>
public static class ShippingFeeCopy
{
    public const string PendingVi = "Phí vận chuyển sẽ được nhân viên thông báo khi xác nhận đơn.";

    public static string EmailNote(decimal fee) =>
        fee > 0 ? $"Phí vận chuyển: {fee:N0} VNĐ." : PendingVi;

    /// <summary>Rewrites the seeded order-email sentence. Leaves other wording unchanged.</summary>
    public static string ReplaceLegacyEmailShipLine(string html)
    {
        const string legacy = " + Ship {{ShippingFee}} VNĐ = ";
        const string current = ". {{ShippingNote}} Tổng tiền hàng: ";
        if (string.IsNullOrEmpty(html) || !html.Contains(legacy, StringComparison.Ordinal))
            return html;
        return html.Replace(legacy, current, StringComparison.Ordinal);
    }

    public static string? ReplaceLegacyTerms(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        foreach (var (oldText, newText) in TermsReplacements)
            html = html.Replace(oldText, newText, StringComparison.Ordinal);
        return html;
    }

    private static readonly (string OldText, string NewText)[] TermsReplacements =
    [
        (
            "phí vận chuyển tính theo tỉnh/thành tại checkout.",
            "phí vận chuyển do nhân viên thông báo khi xác nhận đơn, không tính trên website."),
        (
            "shipping is calculated by province at checkout.",
            "shipping is quoted by staff when the order is confirmed and is not calculated on the website."),
        (
            "送料はチェックアウト時に都道府県で計算します。",
            "送料は注文確認時にスタッフから案内し、サイト上では計算しません。")
    ];
}
