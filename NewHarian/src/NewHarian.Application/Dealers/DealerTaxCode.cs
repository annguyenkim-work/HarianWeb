using System.Text.RegularExpressions;
using NewHarian.Domain.Enums;

namespace NewHarian.Application.Dealers;

/// <summary>
/// MST rules: company = enterprise code (10 digits, branch 10-3);
/// household = owner's CCCD (12 digits, since 2025-07-01) or legacy 10-digit MST.
/// </summary>
public static partial class DealerTaxCode
{
    public const int MaxLength = 20;

    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex CompanyRegex();

    [GeneratedRegex(@"^(\d{10}|\d{12})$")]
    private static partial Regex HouseholdRegex();

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    public static bool IsValid(DealerBusinessType type, string? value)
    {
        var v = Normalize(value);
        return type switch
        {
            DealerBusinessType.Company => CompanyRegex().IsMatch(v),
            DealerBusinessType.Household => HouseholdRegex().IsMatch(v),
            _ => false
        };
    }
}
