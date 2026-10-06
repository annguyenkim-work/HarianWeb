namespace NewHarian.E2E.Fixtures;

/// <summary>Environment-driven E2E configuration (see docs/engineering/testing.md, Phase 2).</summary>
public static class E2ESettings
{
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=newharian_e2e;Username=postgres;Password=postgres";

    /// <summary>Target an already-running app (env ASPNETCORE_ENVIRONMENT=E2E) instead of hosting it in-process.</summary>
    public static string? BaseUrl => Env("E2E_BASE_URL")?.TrimEnd('/');

    public static string ConnectionString => Env("E2E_CONNECTION_STRING") ?? DefaultConnectionString;

    /// <summary>CI sets this: an unreachable DB fails the run instead of skipping it.</summary>
    public static bool Required => IsTrue(Env("E2E_REQUIRED"));

    public static bool Headed => IsTrue(Env("E2E_HEADED"));

    public static float? SlowMoMs => float.TryParse(Env("E2E_SLOWMO_MS"), out var ms) ? ms : null;

    public static string ArtifactsDir => Env("E2E_ARTIFACTS_DIR")
        ?? Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    public static string AdminEmail => "admin@harian.local";
    public static string AdminPassword => Env("E2E_ADMIN_PASSWORD") ?? "Admin@12345";

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsTrue(string? value)
        => value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}
