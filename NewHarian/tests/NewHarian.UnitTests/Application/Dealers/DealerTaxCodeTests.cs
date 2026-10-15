using NewHarian.Application.Dealers;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Dealers;

[Trait("Category", "Unit")]
public class DealerTaxCodeTests
{
    [Theory]
    [InlineData(DealerBusinessType.Company, "0101234567", true)]
    [InlineData(DealerBusinessType.Company, "0101234567-001", true)]
    [InlineData(DealerBusinessType.Company, " 0101 234 567 ", true)]
    [InlineData(DealerBusinessType.Company, "010123456", false)]
    [InlineData(DealerBusinessType.Company, "001234567890", false)]
    [InlineData(DealerBusinessType.Company, "0101234567-01", false)]
    [InlineData(DealerBusinessType.Company, "01012345AB", false)]
    [InlineData(DealerBusinessType.Household, "001234567890", true)]
    [InlineData(DealerBusinessType.Household, "8012345678", true)]
    [InlineData(DealerBusinessType.Household, "0101234567-001", false)]
    [InlineData(DealerBusinessType.Household, "12345678901", false)]
    [InlineData(DealerBusinessType.Household, "", false)]
    [InlineData((DealerBusinessType)0, "0101234567", false)]
    public void IsValid_follows_rules_per_business_type(DealerBusinessType type, string value, bool expected)
    {
        Assert.Equal(expected, DealerTaxCode.IsValid(type, value));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData(" 0101 234567-001 ", "0101234567-001")]
    public void Normalize_strips_all_whitespace(string? input, string expected)
    {
        Assert.Equal(expected, DealerTaxCode.Normalize(input));
    }
}
