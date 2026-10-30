using NewHarian.Application.Hr;

namespace NewHarian.UnitTests.Application.Hr;

[Trait("Category", "Unit")]
public class JobPositionPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateName_rejects_blank(string? name)
    {
        Assert.Equal(JobPositionPolicy.NameRequired, JobPositionPolicy.ValidateName(name));
    }

    [Fact]
    public void ValidateName_rejects_too_long()
    {
        Assert.Equal(JobPositionPolicy.NameTooLong, JobPositionPolicy.ValidateName(new string('a', 101)));
    }

    [Fact]
    public void ValidateName_accepts_trimmed_text()
    {
        Assert.Null(JobPositionPolicy.ValidateName("  Kinh doanh  "));
    }

    [Fact]
    public void IsDuplicate_matches_other_rows_ignoring_case_and_keeps_self()
    {
        var existing = new (int Id, string Name)[] { (1, "Kinh doanh"), (2, "Kho") };

        Assert.True(JobPositionPolicy.IsDuplicate(" kinh doanh ", existing, selfId: null));
        Assert.True(JobPositionPolicy.IsDuplicate("KHO", existing, selfId: 1));
        Assert.False(JobPositionPolicy.IsDuplicate("Kinh doanh", existing, selfId: 1));
        Assert.False(JobPositionPolicy.IsDuplicate("Kế toán", existing, selfId: null));
    }

    [Fact]
    public void Choices_hide_inactive_except_the_one_already_on_the_profile()
    {
        var all = new[]
        {
            new JobPositionChoice(2, "Kho", false, 2),
            new JobPositionChoice(1, "Kinh doanh", true, 1),
            new JobPositionChoice(3, "Kế toán", false, 3)
        };

        var open = JobPositionPolicy.Choices(all, includeId: null);
        Assert.Equal([1], open.Select(p => p.Id).ToArray());

        var keeping = JobPositionPolicy.Choices(all, includeId: 3);
        Assert.Equal([1, 3], keeping.Select(p => p.Id).ToArray());
        Assert.Equal("Kế toán", keeping.Single(p => p.Id == 3).Name);
    }

    [Fact]
    public void ValidateAssignment_allows_empty_active_and_the_current_inactive_row()
    {
        Assert.Null(JobPositionPolicy.ValidateAssignment(true, true, null, null));
        Assert.Null(JobPositionPolicy.ValidateAssignment(true, true, 1, null));
        Assert.Null(JobPositionPolicy.ValidateAssignment(true, false, 4, 4));
        Assert.Equal(JobPositionPolicy.Missing, JobPositionPolicy.ValidateAssignment(false, false, 9, null));
        Assert.Equal(JobPositionPolicy.Inactive, JobPositionPolicy.ValidateAssignment(true, false, 4, null));
        Assert.Equal(JobPositionPolicy.Inactive, JobPositionPolicy.ValidateAssignment(true, false, 4, 2));
    }
}
