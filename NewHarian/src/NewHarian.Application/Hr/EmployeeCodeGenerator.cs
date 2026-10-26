using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NewHarian.Application.Hr;

/// <summary>
/// Name-based employee codes: last given name (TitleCase, no diacritics) + initials of preceding parts.
/// e.g. Nguyễn Kim An → AnNK; Văn Thị Thu Thủy → ThuyVTT. Collisions append 2, 3, …
/// </summary>
public static partial class EmployeeCodeGenerator
{
    public const int MaxLength = 20;

    /// <summary>Base code without numeric suffix (empty when name has no letters).</summary>
    public static string BaseFromFullName(string? fullName)
    {
        var parts = SplitName(fullName);
        if (parts.Count == 0) return string.Empty;

        var given = TitleCaseAscii(parts[^1]);
        if (given.Length == 0) return string.Empty;

        if (parts.Count == 1) return Truncate(given);

        var initials = new StringBuilder();
        for (var i = 0; i < parts.Count - 1; i++)
        {
            var p = parts[i];
            if (p.Length > 0) initials.Append(char.ToUpperInvariant(p[0]));
        }

        return Truncate(given + initials);
    }

    /// <summary>Allocates a unique code among <paramref name="existingCodes"/> (case-insensitive).</summary>
    public static string Allocate(string? fullName, IEnumerable<string> existingCodes)
    {
        var taken = new HashSet<string>(
            existingCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var baseCode = BaseFromFullName(fullName);
        if (baseCode.Length == 0) baseCode = "NV";

        if (!taken.Contains(baseCode)) return baseCode;

        for (var n = 2; n < 10_000; n++)
        {
            var candidate = Truncate($"{baseCode}{n}");
            if (!taken.Contains(candidate)) return candidate;
        }

        throw new InvalidOperationException("Không cấp được mã nhân viên duy nhất.");
    }

    private static List<string> SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return [];
        return MultiSpace().Split(RemoveDiacritics(fullName.Trim()))
            .Select(p => LettersOnly().Replace(p, ""))
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static string RemoveDiacritics(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }

    private static string TitleCaseAscii(string s)
    {
        if (s.Length == 0) return s;
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    private static string Truncate(string code)
        => code.Length <= MaxLength ? code : code[..MaxLength];

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"[^A-Za-z]", RegexOptions.CultureInvariant)]
    private static partial Regex LettersOnly();
}
