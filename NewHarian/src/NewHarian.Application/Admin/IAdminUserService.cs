namespace NewHarian.Application.Admin;

public sealed record AdminUserListItemDto(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);

public sealed class AdminUserSaveRequest
{
    /// <summary>Null = create.</summary>
    public string? Id { get; set; }
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    /// <summary>Required on create; on edit, empty keeps the current password.</summary>
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = [];
}

/// <summary>Super Admin management of internal accounts: create, roles, activate/deactivate, reset password.</summary>
public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserListItemDto>> ListAsync(CancellationToken ct = default);

    Task<AdminUserSaveRequest?> GetForEditAsync(string id, CancellationToken ct = default);

    /// <param name="actorUserId">Signed-in Super Admin; cannot deactivate or demote themselves.</param>
    Task<(bool Ok, string? Error, string? Id)> SaveAsync(AdminUserSaveRequest request, string? actorUserId, CancellationToken ct = default);
}
