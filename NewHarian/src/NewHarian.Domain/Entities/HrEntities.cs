using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

/// <summary>
/// HR record of an internal account. One profile per user (unique <see cref="UserId"/>); full name lives on the user,
/// and the user's role stands in for department / position.
/// </summary>
public class EmployeeProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}
