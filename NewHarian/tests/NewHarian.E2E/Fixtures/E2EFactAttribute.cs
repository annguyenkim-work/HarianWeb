namespace NewHarian.E2E.Fixtures;

/// <summary>
/// Fact that skips (locally) when PostgreSQL is unreachable. With E2E_REQUIRED=true (CI) it never skips,
/// so a missing database fails the run instead of silently passing.
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> SkipReason = new(ComputeSkipReason);

    public E2EFactAttribute()
    {
        if (SkipReason.Value is { } reason)
            Skip = reason;
    }

    private static string? ComputeSkipReason()
    {
        if (E2ESettings.Required) return null;
        var error = E2EDatabase.Probe(E2ESettings.ConnectionString);
        return error is null
            ? null
            : $"E2E skipped: PostgreSQL not reachable ({error}). Set E2E_CONNECTION_STRING (database name must contain 'e2e') " +
              "or E2E_REQUIRED=true to fail instead. See docs/engineering/testing.md.";
    }
}
