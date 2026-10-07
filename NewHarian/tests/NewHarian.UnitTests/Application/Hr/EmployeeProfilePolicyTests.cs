using NewHarian.Application.Hr;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Hr;

[Trait("Category", "Unit")]
public class EmployeeProfilePolicyTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    [Theory]
    [InlineData(new string[0], "NV0001")]
    [InlineData(new[] { "NV0001" }, "NV0002")]
    [InlineData(new[] { "NV0003", "NV0010", "NV0002" }, "NV0011")]
    [InlineData(new[] { "nv0007" }, "NV0008")]
    [InlineData(new[] { "X0099", "NVabc", "NV", "NV-5" }, "NV0001")]
    [InlineData(new[] { "NV9999" }, "NV10000")]
    public void NextCode_continues_after_highest_numeric_code(string[] existing, string expected)
    {
        Assert.Equal(expected, EmployeeProfilePolicy.NextCode(existing));
    }

    [Fact]
    public void Validate_accepts_minimal_form()
    {
        Assert.Null(EmployeeProfilePolicy.Validate(new EmployeeProfileForm { FullName = "An" }, EmployeeEditScope.Hr, Today));
    }

    public static TheoryData<Action<EmployeeProfileForm>, string> InvalidPersonal => new()
    {
        { f => f.FullName = " ", EmployeeProfilePolicy.FullNameRequired },
        { f => f.Phone = "abc", EmployeeProfilePolicy.PhoneInvalid },
        { f => f.EmergencyContactPhone = "12", EmployeeProfilePolicy.EmergencyPhoneInvalid },
        { f => f.PersonalEmail = "not-an-email", EmployeeProfilePolicy.PersonalEmailInvalid },
        { f => f.CitizenId = "12345", EmployeeProfilePolicy.CitizenIdInvalid },
        { f => f.DateOfBirth = Today.AddYears(-14), EmployeeProfilePolicy.DateOfBirthInvalid },
        { f => f.DateOfBirth = new DateOnly(1899, 12, 31), EmployeeProfilePolicy.DateOfBirthInvalid },
        { f => f.Address = new string('a', 501), EmployeeProfilePolicy.TooLong },
    };

    [Theory]
    [MemberData(nameof(InvalidPersonal))]
    public void Validate_rejects_bad_personal_fields_for_both_scopes(Action<EmployeeProfileForm> breakIt, string expected)
    {
        var form = new EmployeeProfileForm { FullName = "An" };
        breakIt(form);

        Assert.Equal(expected, EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Self, Today));
        Assert.Equal(expected, EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Hr, Today));
    }

    [Theory]
    [InlineData("001099012345")]
    [InlineData("001 099 012 345")]
    [InlineData("123456789")]
    public void Validate_accepts_cmnd_and_cccd(string citizenId)
    {
        var form = new EmployeeProfileForm { FullName = "An", CitizenId = citizenId, DateOfBirth = Today.AddYears(-15) };

        Assert.Null(EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Self, Today));
    }

    [Fact]
    public void Validate_rejects_termination_before_hire_for_hr()
    {
        var form = new EmployeeProfileForm
        {
            FullName = "An", HireDate = new DateOnly(2026, 5, 1), TerminationDate = new DateOnly(2026, 4, 30)
        };

        Assert.Equal(EmployeeProfilePolicy.TerminationBeforeHire, EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Hr, Today));
    }

    [Fact]
    public void Validate_requires_termination_date_when_resigned()
    {
        var form = new EmployeeProfileForm { FullName = "An", Status = EmployeeStatus.Resigned };

        Assert.Equal(EmployeeProfilePolicy.TerminationRequired, EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Hr, Today));
    }

    [Fact]
    public void Validate_rejects_too_long_hr_notes_for_hr()
    {
        var form = new EmployeeProfileForm { FullName = "An", HrNotes = new string('x', EmployeeProfilePolicy.NotesMax + 1) };

        Assert.Equal(EmployeeProfilePolicy.TooLong, EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Hr, Today));
    }

    [Fact]
    public void Validate_skips_hr_rules_for_self_because_hr_fields_are_ignored()
    {
        var form = new EmployeeProfileForm
        {
            FullName = "An", Status = EmployeeStatus.Resigned, HrNotes = new string('x', EmployeeProfilePolicy.NotesMax + 1)
        };

        Assert.Null(EmployeeProfilePolicy.Validate(form, EmployeeEditScope.Self, Today));
    }

    [Fact]
    public void Apply_self_never_changes_hr_fields()
    {
        var profile = StoredProfile();
        var form = PostedForm();

        EmployeeProfilePolicy.Apply(profile, form, EmployeeEditScope.Self);

        Assert.Equal("0909123456", profile.Phone);
        Assert.Equal(new DateOnly(1995, 3, 2), profile.DateOfBirth);
        Assert.Equal(EmployeeStatus.Active, profile.Status);
        Assert.Equal(new DateOnly(2024, 1, 2), profile.HireDate);
        Assert.Null(profile.TerminationDate);
        Assert.Equal("note", profile.HrNotes);
    }

    [Fact]
    public void Apply_hr_changes_every_field_and_normalizes_text()
    {
        var profile = StoredProfile();
        var form = PostedForm();

        EmployeeProfilePolicy.Apply(profile, form, EmployeeEditScope.Hr);

        Assert.Equal(new DateOnly(2025, 2, 3), profile.HireDate);
        Assert.Equal(EmployeeStatus.Resigned, profile.Status);
        Assert.Equal(new DateOnly(2026, 9, 30), profile.TerminationDate);
        Assert.Null(profile.HrNotes);
        Assert.Equal("001099012345", profile.CitizenId);
        Assert.Null(profile.PersonalEmail);
    }

    [Fact]
    public void MergeReadOnly_restores_identity_and_hr_fields_for_self()
    {
        var posted = PostedForm();
        posted.Email = "hacked@x.local";
        posted.EmployeeCode = "NV9999";
        var stored = new EmployeeProfileForm
        {
            UserId = "u1", Email = "real@x.local", EmployeeCode = "NV0001", Roles = ["HrStaff"],
            HireDate = new DateOnly(2023, 6, 1), Status = EmployeeStatus.Active, HrNotes = "note"
        };

        EmployeeProfilePolicy.MergeReadOnly(posted, stored, EmployeeEditScope.Self);

        Assert.Equal("real@x.local", posted.Email);
        Assert.Equal("NV0001", posted.EmployeeCode);
        Assert.Equal(["HrStaff"], posted.Roles);
        Assert.Equal(new DateOnly(2023, 6, 1), posted.HireDate);
        Assert.Null(posted.TerminationDate);
        Assert.Equal("note", posted.HrNotes);
        Assert.Equal(EmployeeStatus.Active, posted.Status);
        Assert.Equal(" 0909123456 ", posted.Phone);
    }

    [Fact]
    public void MergeReadOnly_keeps_posted_hr_fields_for_hr()
    {
        var posted = PostedForm();
        var stored = new EmployeeProfileForm { UserId = "u1", Email = "real@x.local", Status = EmployeeStatus.Active };

        EmployeeProfilePolicy.MergeReadOnly(posted, stored, EmployeeEditScope.Hr);

        Assert.Equal("real@x.local", posted.Email);
        Assert.Equal(EmployeeStatus.Resigned, posted.Status);
        Assert.Equal(new DateOnly(2026, 9, 30), posted.TerminationDate);
    }

    [Fact]
    public void IsPersonalComplete_needs_dob_phone_address_and_citizen_id()
    {
        Assert.False(EmployeeProfilePolicy.IsPersonalComplete(null));
        var profile = new EmployeeProfile { DateOfBirth = new DateOnly(1995, 1, 1), Phone = "0909", Address = "HN" };
        Assert.False(EmployeeProfilePolicy.IsPersonalComplete(profile));

        profile.CitizenId = "001099012345";
        Assert.True(EmployeeProfilePolicy.IsPersonalComplete(profile));
    }

    [Fact]
    public void Every_status_and_gender_has_a_vietnamese_label()
    {
        Assert.All(EmployeeProfilePolicy.Statuses, s => Assert.NotEqual(s.ToString(), EmployeeProfilePolicy.StatusLabel(s)));
        Assert.All(EmployeeProfilePolicy.Genders, g => Assert.NotEqual(g.ToString(), EmployeeProfilePolicy.GenderLabel(g)));
    }

    private static EmployeeProfile StoredProfile() => new()
    {
        EmployeeCode = "NV0001",
        Status = EmployeeStatus.Active,
        HireDate = new DateOnly(2024, 1, 2),
        HrNotes = "note"
    };

    private static EmployeeProfileForm PostedForm() => new()
    {
        UserId = "u1",
        FullName = "An",
        DateOfBirth = new DateOnly(1995, 3, 2),
        Phone = " 0909123456 ",
        PersonalEmail = "  ",
        CitizenId = "001 099 012 345",
        Status = EmployeeStatus.Resigned,
        HireDate = new DateOnly(2025, 2, 3),
        TerminationDate = new DateOnly(2026, 9, 30),
        HrNotes = ""
    };
}
