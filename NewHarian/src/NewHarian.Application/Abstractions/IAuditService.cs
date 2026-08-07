namespace NewHarian.Application.Abstractions;

public interface IAuditService
{
    /// <summary>Fire-and-forget friendly write; does not throw to callers — logs on failure.</summary>
    Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object? oldValues,
        object? newValues,
        CancellationToken ct = default);
}
