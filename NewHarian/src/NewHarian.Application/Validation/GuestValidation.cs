using System.Net.Mail;
using System.Text.RegularExpressions;

namespace NewHarian.Application.Validation;

/// <summary>
/// Shared validation primitives for guest-facing forms
/// (Contact, Careers, Checkout, Booking, Track) — see docs/validation-matrix.md.
/// </summary>
public static partial class GuestValidation
{
    public const int NameMax = 200;
    public const int EmailMax = 200;
    public const int AddressMax = 500;
    public const int MessageMax = 5000;
    public const int NotesMax = 2000;
    public const int PublicCodeMax = 32;

    [GeneratedRegex(@"^[\d\s+\-]{8,20}$")]
    private static partial Regex PhoneRegex();

    /// <summary>New HAR-ORDER-0001 or legacy HAR-YYYYMMDD-XXXX.</summary>
    [GeneratedRegex(@"^(HAR-ORDER-\d{4,}|[A-Za-z]{2,5}-\d{8}-\w{1,8})$", RegexOptions.IgnoreCase)]
    private static partial Regex OrderNumberRegex();

    /// <summary>RFC-parseable email, trimmed, max length (spec: 200 for guest forms).</summary>
    public static bool IsEmail(string? value, int maxLength = EmailMax)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v) || v.Length > maxLength) return false;
        try { _ = new MailAddress(v); return true; }
        catch { return false; }
    }

    /// <summary>Phone charset 8–20 (`^[\d\s+\-]{8,20}$`). Empty/null counts as valid — check Required separately.</summary>
    public static bool IsPhone(string? value)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) || PhoneRegex().IsMatch(v);
    }

    /// <summary>Required string with trimmed length in [min, max].</summary>
    public static bool HasLength(string? value, int min, int max)
    {
        var len = value?.Trim().Length ?? 0;
        return len >= min && len <= max;
    }

    /// <summary>Optional string: valid when empty or trimmed length ≤ max.</summary>
    public static bool FitsMax(string? value, int max)
        => (value?.Trim().Length ?? 0) <= max;

    /// <summary>Guest order number: HAR-ORDER-XXXX (also accepts legacy HAR-YYYYMMDD-XXXX).</summary>
    public static bool IsOrderNumber(string? value)
    {
        var v = value?.Trim();
        return !string.IsNullOrEmpty(v) && v.Length <= PublicCodeMax && OrderNumberRegex().IsMatch(v);
    }
}
