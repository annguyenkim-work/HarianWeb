using Npgsql;

namespace NewHarian.E2E.Fixtures;

public static class E2EDatabase
{
    /// <summary>Returns null when the server accepts connections, otherwise a human-readable reason.</summary>
    public static string? Probe(string connectionString)
    {
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Timeout = 3, Pooling = false };
            using var conn = new NpgsqlConnection(csb.ConnectionString);
            conn.Open();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Drops the E2E database; the app's DbSeeder (MigrateAsync) recreates it on start.</summary>
    public static async Task DropAsync(string connectionString)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var name = csb.Database ?? throw new InvalidOperationException("E2E connection string has no Database.");
        // Guard against pointing the suite at a real database: it is dropped on every run.
        if (!name.Contains("e2e", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to drop database '{name}': E2E database name must contain 'e2e'.");

        csb.Database = "postgres";
        csb.Pooling = false;
        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{name.Replace("\"", "\"\"")}\" WITH (FORCE)";
        await cmd.ExecuteNonQueryAsync();
    }
}
