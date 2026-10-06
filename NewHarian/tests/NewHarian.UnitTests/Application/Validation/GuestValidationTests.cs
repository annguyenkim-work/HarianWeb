using NewHarian.Application.Validation;

namespace NewHarian.UnitTests.Application.Validation;

[Trait("Category", "Unit")]
public class GuestValidationTests
{
    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("+84 912 345 678", true)]
    [InlineData("091-234-5678", true)]
    [InlineData("12345678", true)]
    [InlineData("12345678901234567890", true)]
    [InlineData("1234567", false)]
    [InlineData("123456789012345678901", false)]
    [InlineData("0912abc678", false)]
    [InlineData("(091) 2345678", false)]
    public void IsPhone_accepts_8_to_20_digits_spaces_plus_and_dash(string phone, bool expected)
    {
        Assert.Equal(expected, GuestValidation.IsPhone(phone));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsPhone_treats_empty_as_valid_so_required_is_checked_separately(string? phone)
    {
        Assert.True(GuestValidation.IsPhone(phone));
    }

    [Theory]
    [InlineData("012345678", true)]
    [InlineData("012345678901", true)]
    [InlineData("0123 4567 8901", true)]
    [InlineData("012-345-678", true)]
    [InlineData("0123456789", false)]
    [InlineData("01234567", false)]
    [InlineData("0123456789012", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCitizenId_requires_9_or_12_digits_after_normalizing(string? value, bool expected)
    {
        Assert.Equal(expected, GuestValidation.IsCitizenId(value));
    }

    [Theory]
    [InlineData(" 012-345-678 ", "012345678")]
    [InlineData("CCCD: 0123 4567 8901", "012345678901")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeCitizenId_keeps_digits_only(string? value, string expected)
    {
        Assert.Equal(expected, GuestValidation.NormalizeCitizenId(value));
    }

    [Theory]
    [InlineData("khach@harian.vn", true)]
    [InlineData("  khach@harian.vn  ", true)]
    [InlineData("first.last+tag@sub.example.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("user@", false)]
    [InlineData("@harian.vn", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsEmail_validates_trimmed_address(string? value, bool expected)
    {
        Assert.Equal(expected, GuestValidation.IsEmail(value));
    }

    [Fact]
    public void IsEmail_rejects_values_longer_than_max()
    {
        var local = new string('a', GuestValidation.EmailMax);
        Assert.False(GuestValidation.IsEmail(local + "@x.vn"));
        Assert.False(GuestValidation.IsEmail("abcdef@x.vn", maxLength: 10));
        Assert.True(GuestValidation.IsEmail("abcde@x.vn", maxLength: 10));
    }

    [Theory]
    [InlineData("HAR-ORDER-0001", true)]
    [InlineData("har-order-0001", true)]
    [InlineData("HAR-ORDER-123456", true)]
    [InlineData("  HAR-ORDER-0042  ", true)]
    [InlineData("HAR-20260713-0003", true)]
    [InlineData("HAR-ORDER-001", false)]
    [InlineData("HAR-ORDER-", false)]
    [InlineData("HAR-SERVICE-0001", false)]
    [InlineData("HARIAN-20260101-1", false)]
    [InlineData("HAR-ORDER-12345678901234567890123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOrderNumber_accepts_new_and_legacy_formats(string? value, bool expected)
    {
        Assert.Equal(expected, GuestValidation.IsOrderNumber(value));
    }

    [Theory]
    [InlineData("abcde", 5, 10, true)]
    [InlineData("  abcde  ", 5, 10, true)]
    [InlineData("abcd", 5, 10, false)]
    [InlineData("abcdefghijk", 5, 10, false)]
    [InlineData(null, 0, 10, true)]
    [InlineData(null, 1, 10, false)]
    public void HasLength_checks_trimmed_length_range(string? value, int min, int max, bool expected)
    {
        Assert.Equal(expected, GuestValidation.HasLength(value, min, max));
    }

    [Theory]
    [InlineData(null, 3, true)]
    [InlineData("", 3, true)]
    [InlineData(" abc ", 3, true)]
    [InlineData("abcd", 3, false)]
    public void FitsMax_allows_empty_or_trimmed_length_within_max(string? value, int max, bool expected)
    {
        Assert.Equal(expected, GuestValidation.FitsMax(value, max));
    }
}
