namespace NewHarian.Application.Hr;

/// <summary>Resolves a staff user's display label for audit / status history (prefer employee code).</summary>
public interface IStaffActorLabel
{
    /// <summary>EmployeeCode, else FullName, else email, else userId.</summary>
    Task<string> ResolveAsync(string? userId, CancellationToken ct = default);
}
