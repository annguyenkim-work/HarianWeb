using System.Globalization;
using System.Text.RegularExpressions;

namespace NewHarian.Application.Formatting;

/// <summary>VND display + parse for UI (vi thousands separator, suffix VNĐ).</summary>
public static partial class MoneyFormat
{
    public const string CurrencySuffix = "VNĐ";

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>e.g. 1500000 → "1.500.000 VNĐ"</summary>
    public static string Vnd(decimal amount)
        => amount.ToString("N0", Vi) + " " + CurrencySuffix;

    public static string Vnd(decimal? amount)
        => amount is null ? "" : Vnd(amount.Value);

    /// <summary>Negative amounts keep the sign: -595000 → "-595.000 VNĐ"</summary>
    public static string VndSigned(decimal amount)
        => amount < 0 ? "-" + Vnd(Math.Abs(amount)) : Vnd(amount);

    /// <summary>
    /// Accepts "1500000", "1.500.000", "1.500.000 VNĐ", "1500000đ".
    /// Does not treat a single "." as a decimal point (VND is whole đồng).
    /// </summary>
    public static bool TryParse(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = SuffixRegex().Replace(raw.Trim(), "").Trim();
        if (s.Length == 0) return false;

        if (GroupedIntegerRegex().IsMatch(s))
            return decimal.TryParse(s.Replace(".", "", StringComparison.Ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        if (PlainIntegerRegex().IsMatch(s))
            return decimal.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        return false;
    }

    [GeneratedRegex(@"\s*(VNĐ|VND|đ|Đ)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SuffixRegex();

    [GeneratedRegex(@"^-?\d{1,3}(\.\d{3})+$")]
    private static partial Regex GroupedIntegerRegex();

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex PlainIntegerRegex();
}
