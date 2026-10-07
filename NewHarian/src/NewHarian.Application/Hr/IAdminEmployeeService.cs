using NewHarian.Domain.Enums;

namespace NewHarian.Application.Hr;

/// <summary>Who is saving: the employee (personal fields only) or HR (every field).</summary>
public enum EmployeeEditScope
{
    Self,
    Hr
}

public sealed record EmployeeListItemDto(
    string UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    string? EmployeeCode,
    EmployeeStatus? Status,
    bool IsPersonalComplete);

public sealed class EmployeeProfileForm
{
    public string UserId { get; set; } = string.Empty;

    // Read-only, shown for context; never applied on save.
    public string Email { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public List<string> Roles { get; set; } = [];

    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public EmployeeGender? Gender { get; set; }
    public string? Phone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Address { get; set; }
    public string? CitizenId { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public DateOnly? HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? HrNotes { get; set; }
}

public interface IAdminEmployeeService
{
    /// <summary>Every internal account, with its profile when one exists.</summary>
    Task<IReadOnlyList<EmployeeListItemDto>> ListAsync(string? q, EmployeeStatus? status, CancellationToken ct = default);

    /// <summary>Null when the user does not exist. A user without a profile yet gets an empty form.</summary>
    Task<EmployeeProfileForm?> GetAsync(string userId, CancellationToken ct = default);

    /// <summary>Creates the profile on first save (auto employee code). Self scope ignores HR fields.</summary>
    Task<(bool Ok, string? Error)> SaveAsync(
        EmployeeProfileForm form, EmployeeEditScope scope, string? actorUserId, CancellationToken ct = default);
}
