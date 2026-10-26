using NewHarian.Application.Hr;

namespace NewHarian.UnitTests.Application.Hr;

[Trait("Category", "Unit")]
public class EmployeeCodeGeneratorTests
{
    [Theory]
    [InlineData("Nguyễn Kim An", "AnNK")]
    [InlineData("Văn Thị Thu Thủy", "ThuyVTT")]
    [InlineData("Trần Hải Hà", "HaTH")]
    [InlineData("An", "An")]
    [InlineData("  lê  văn   đức  ", "DucLV")]
    public void BaseFromFullName_matches_examples(string fullName, string expected)
        => Assert.Equal(expected, EmployeeCodeGenerator.BaseFromFullName(fullName));

    [Fact]
    public void Allocate_uses_base_when_free()
        => Assert.Equal("AnNK", EmployeeCodeGenerator.Allocate("Nguyễn Kim An", ["NV0001", "ThuyVTT"]));

    [Fact]
    public void Allocate_appends_suffix_on_collision()
    {
        var existing = new[] { "AnNK", "AnNK2" };
        Assert.Equal("AnNK3", EmployeeCodeGenerator.Allocate("Nguyễn Kim An", existing));
    }

    [Fact]
    public void Allocate_is_case_insensitive_against_existing()
        => Assert.Equal("AnNK2", EmployeeCodeGenerator.Allocate("Nguyễn Kim An", ["annk"]));

    [Fact]
    public void BaseFromFullName_empty_when_no_letters()
        => Assert.Equal("", EmployeeCodeGenerator.BaseFromFullName("123 ---"));
}
