using NewHarian.Domain.Enums;

namespace NewHarian.Application.Hr;

/// <summary>Who is saving: the employee (personal fields only) or HR (every field).</summary>
public enum EmployeeEditScope
{
    Self,
    Hr
}

public sealed record EmployeeListItemDto(
    int? ProfileId,
    string? UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    string? EmployeeCode,
    EmployeeStatus? Status,
    bool IsPersonalComplete,
    bool HasAccount);

public sealed record EmployeeLinkOptionDto(int Id, string EmployeeCode, string FullName, string? PersonalEmail);

public sealed class EmployeeProfileForm
{
    /// <summary>Null = create new profile (HR).</summary>
    public int? Id { get; set; }
    public string? UserId { get; set; }

    // Read-only, shown for context; never applied on save.
    public string Email { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool HasAccount { get; set; }

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

public sealed class GrantEmployeeAccountRequest
{
    public int ProfileId { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public interface IAdminEmployeeService
{
    Task<IReadOnlyList<EmployeeListItemDto>> ListAsync(string? q, EmployeeStatus? status, CancellationToken ct = default);

    Task<EmployeeProfileForm?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>By login user id (MyProfile / legacy). Null when the user does not exist.</summary>
    Task<EmployeeProfileForm?> GetByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>Creates when <see cref="EmployeeProfileForm.Id"/> is null; updates otherwise. Self scope ignores HR fields.</summary>
    Task<(bool Ok, string? Error, int? Id)> SaveAsync(
        EmployeeProfileForm form, EmployeeEditScope scope, string? actorUserId, CancellationToken ct = default);

    /// <summary>Creates an Identity user and links it to the profile.</summary>
    Task<(bool Ok, string? Error)> GrantAccountAsync(
        GrantEmployeeAccountRequest request, string? actorUserId, CancellationToken ct = default);

    /// <summary>Profiles with no account, plus <paramref name="includeProfileId"/> when editing a linked user.</summary>
    Task<IReadOnlyList<EmployeeLinkOptionDto>> ListLinkOptionsAsync(int? includeProfileId, CancellationToken ct = default);
}
