using System.Security.Cryptography;
using System.Text;

namespace NewHarian.Application.Dealers;

/// <summary>
/// Secret dealer code <c>HRN-XXXX-XXXX-XXXX</c>: 12 Crockford Base32 chars (60 random bits) from the OS CSPRNG.
/// Hashes and comparisons always use the 12-char normalized form.
/// </summary>
public static class DealerAccessCode
{
    public const string Prefix = "HRN";
    public const int Length = 12;
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);

    /// <summary>Accepts any case, spaces, dashes, optional prefix and O/I/L look-alikes; null when not a well-formed code.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 64) return null;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch) || ch == '-') continue;
            sb.Append(char.ToUpperInvariant(ch));
        }

        var s = sb.ToString();
        if (s.Length == Prefix.Length + Length && s.StartsWith(Prefix, StringComparison.Ordinal))
            s = s[Prefix.Length..];
        if (s.Length != Length) return null;

        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                var c => c
            };
            if (Alphabet.IndexOf(chars[i]) < 0) return null;
        }
        return new string(chars);
    }

    public static string Format(string normalized) =>
        $"{Prefix}-{normalized[..4]}-{normalized[4..8]}-{normalized[8..]}";

    public static string Last4(string normalized) => normalized[^4..];

    public static string Masked(string last4) => $"{Prefix}-••••-••••-{last4}";
}
