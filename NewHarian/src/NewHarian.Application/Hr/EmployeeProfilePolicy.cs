using System.Globalization;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;

namespace NewHarian.Application.Hr;

public static class EmployeeProfilePolicy
{
    public const string CodePrefix = "NV";
    public const int MinAge = 15;
    public const int ShortTextMax = 200;
    public const int NotesMax = 2000;

    public const string FullNameRequired = "Họ tên bắt buộc.";
    public const string PhoneInvalid = "Số điện thoại không hợp lệ.";
    public const string EmergencyPhoneInvalid = "Số điện thoại người liên hệ khẩn cấp không hợp lệ.";
    public const string PersonalEmailInvalid = "Email cá nhân không hợp lệ.";
    public const string CitizenIdInvalid = "CCCD phải gồm 9 hoặc 12 chữ số.";
    public const string DateOfBirthInvalid = "Nhân viên phải từ 15 tuổi trở lên.";
    public const string TooLong = "Nội dung quá dài.";
    public const string TerminationBeforeHire = "Ngày nghỉ việc phải sau ngày vào làm.";
    public const string TerminationRequired = "Nhập ngày nghỉ việc khi trạng thái là Đã nghỉ việc.";

    public static IReadOnlyList<EmployeeStatus> Statuses { get; } = Enum.GetValues<EmployeeStatus>();
    public static IReadOnlyList<EmployeeGender> Genders { get; } = Enum.GetValues<EmployeeGender>();

    public static string StatusLabel(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Probation => "Thử việc",
        EmployeeStatus.Active => "Đang làm việc",
        EmployeeStatus.OnLeave => "Tạm nghỉ",
        EmployeeStatus.Resigned => "Đã nghỉ việc",
        _ => status.ToString()
    };

    public static string GenderLabel(EmployeeGender gender) => gender switch
    {
        EmployeeGender.Male => "Nam",
        EmployeeGender.Female => "Nữ",
        EmployeeGender.Other => "Khác",
        _ => gender.ToString()
    };

    /// <summary>NV0001, NV0002, … Codes that do not match the prefix + digits pattern are ignored.</summary>
    public static string NextCode(IEnumerable<string> existingCodes)
    {
        var max = 0;
        foreach (var code in existingCodes)
        {
            if (code is null || !code.StartsWith(CodePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(code.AsSpan(CodePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > max)
                max = n;
        }
        return $"{CodePrefix}{max + 1:D4}";
    }

    /// <summary>First validation error, or null. HR-only rules are checked only for <see cref="EmployeeEditScope.Hr"/>.</summary>
    public static string? Validate(EmployeeProfileForm form, EmployeeEditScope scope, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(form.FullName)) return FullNameRequired;
        if (!GuestValidation.FitsMax(form.FullName, GuestValidation.NameMax)
            || !GuestValidation.FitsMax(form.Address, GuestValidation.AddressMax)
            || !GuestValidation.FitsMax(form.EmergencyContactName, ShortTextMax))
            return TooLong;
        if (!GuestValidation.IsPhone(form.Phone)) return PhoneInvalid;
        if (!string.IsNullOrWhiteSpace(form.PersonalEmail) && !GuestValidation.IsEmail(form.PersonalEmail))
            return PersonalEmailInvalid;
        if (!string.IsNullOrWhiteSpace(form.CitizenId) && !GuestValidation.IsCitizenId(form.CitizenId))
            return CitizenIdInvalid;
        if (form.DateOfBirth is { } dob && (dob > today.AddYears(-MinAge) || dob.Year < 1900))
            return DateOfBirthInvalid;
        if (!GuestValidation.IsPhone(form.EmergencyContactPhone)) return EmergencyPhoneInvalid;

        if (scope != EmployeeEditScope.Hr) return null;

        if (!GuestValidation.FitsMax(form.HrNotes, NotesMax)) return TooLong;
        if (form.HireDate is { } hire && form.TerminationDate is { } end && end < hire) return TerminationBeforeHire;
        if (form.Status == EmployeeStatus.Resigned && form.TerminationDate is null) return TerminationRequired;
        return null;
    }

    /// <summary>Copies the form into the entity. Self scope never touches HR fields, whatever was posted.</summary>
    public static void Apply(EmployeeProfile profile, EmployeeProfileForm form, EmployeeEditScope scope)
    {
        profile.DateOfBirth = form.DateOfBirth;
        profile.Gender = form.Gender;
        profile.Phone = Clean(form.Phone);
        profile.PersonalEmail = Clean(form.PersonalEmail);
        profile.Address = Clean(form.Address);
        profile.CitizenId = Clean(GuestValidation.NormalizeCitizenId(form.CitizenId));
        profile.EmergencyContactName = Clean(form.EmergencyContactName);
        profile.EmergencyContactPhone = Clean(form.EmergencyContactPhone);

        if (scope != EmployeeEditScope.Hr) return;

        profile.HireDate = form.HireDate;
        profile.TerminationDate = form.TerminationDate;
        profile.Status = form.Status;
        profile.HrNotes = Clean(form.HrNotes);
    }

    /// <summary>
    /// Restores what the posted form cannot be trusted with (email, code, roles; HR fields for self)
    /// so a re-rendered form after a validation error shows stored values.
    /// </summary>
    public static void MergeReadOnly(EmployeeProfileForm posted, EmployeeProfileForm stored, EmployeeEditScope scope)
    {
        posted.UserId = stored.UserId;
        posted.Email = stored.Email;
        posted.EmployeeCode = stored.EmployeeCode;
        posted.Roles = stored.Roles;

        if (scope == EmployeeEditScope.Hr) return;

        posted.HireDate = stored.HireDate;
        posted.TerminationDate = stored.TerminationDate;
        posted.Status = stored.Status;
        posted.HrNotes = stored.HrNotes;
    }

    /// <summary>The employee has filled in the fields HR needs (date of birth, phone, address, ID number).</summary>
    public static bool IsPersonalComplete(EmployeeProfile? profile)
        => profile is not null
           && profile.DateOfBirth is not null
           && !string.IsNullOrWhiteSpace(profile.Phone)
           && !string.IsNullOrWhiteSpace(profile.Address)
           && !string.IsNullOrWhiteSpace(profile.CitizenId);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
