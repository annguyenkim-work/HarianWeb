using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NewHarian.Application.Abstractions;

public static partial class SlugHelper
{
    public static string FromVietnamese(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = input.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var s = sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'd')
            .ToLowerInvariant();

        s = NonSlugChars().Replace(s, " ");
        s = MultiSpace().Replace(s, " ").Trim();
        s = s.Replace(' ', '-');
        s = MultiDash().Replace(s, "-").Trim('-');
        return s;
    }

    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"-+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiDash();
}
