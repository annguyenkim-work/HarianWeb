namespace NewHarian.Application.Abstractions;

/// <summary>Guest-facing public codes: HAR-ORDER-0001, HAR-SERVICE-0001.</summary>
public static class PublicReferenceCodes
{
    public const string OrderPrefix = "HAR-ORDER-";
    public const string ServicePrefix = "HAR-SERVICE-";
    public const int MaxLength = 32;

    public static string Format(string prefix, int sequence) => $"{prefix}{sequence:0000}";

    public static int NextSequence(IEnumerable<string> existingCodes, string prefix)
    {
        var max = 0;
        foreach (var code in existingCodes)
        {
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code.AsSpan(prefix.Length), out var n)
                && n > max)
                max = n;
        }

        return max + 1;
    }
}
