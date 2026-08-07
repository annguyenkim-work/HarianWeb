namespace NewHarian.Domain.Entities;

/// <summary>Persisted application log row (ILogger → DB via DbLoggerProvider).</summary>
public class AppLogEntry
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Maps to <see cref="Microsoft.Extensions.Logging.LogLevel"/> (0–6).</summary>
    public short Level { get; set; }
    /// <summary>Short type name from logger category (e.g. OrderService).</summary>
    public string Module { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
