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

    /// <summary>
    /// Drops and recreates the E2E database. MigrateAsync applies schema but does not CREATE DATABASE
    /// on Postgres, so an empty DB must exist before the app starts.
    /// </summary>
    public static async Task DropAsync(string connectionString)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var name = csb.Database ?? throw new InvalidOperationException("E2E connection string has no Database.");
        // Guard against pointing the suite at a real database: it is dropped on every run.
        if (!name.Contains("e2e", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to drop database '{name}': E2E database name must contain 'e2e'.");

        var quoted = "\"" + name.Replace("\"", "\"\"") + "\"";
        csb.Database = "postgres";
        csb.Pooling = false;
        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        await using (var drop = conn.CreateCommand())
        {
            drop.CommandText = $"DROP DATABASE IF EXISTS {quoted} WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
        await using (var create = conn.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE {quoted}";
            await create.ExecuteNonQueryAsync();
        }
    }
}
