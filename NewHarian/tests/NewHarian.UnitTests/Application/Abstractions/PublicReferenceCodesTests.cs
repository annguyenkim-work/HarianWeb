using NewHarian.Application.Abstractions;

namespace NewHarian.UnitTests.Application.Abstractions;

[Trait("Category", "Unit")]
public class PublicReferenceCodesTests
{
    [Theory]
    [InlineData(PublicReferenceCodes.OrderPrefix, 1, "HAR-ORDER-0001")]
    [InlineData(PublicReferenceCodes.OrderPrefix, 9999, "HAR-ORDER-9999")]
    [InlineData(PublicReferenceCodes.OrderPrefix, 12345, "HAR-ORDER-12345")]
    [InlineData(PublicReferenceCodes.ServicePrefix, 42, "HAR-SERVICE-0042")]
    public void Format_pads_sequence_to_four_digits(string prefix, int sequence, string expected)
    {
        Assert.Equal(expected, PublicReferenceCodes.Format(prefix, sequence));
    }

    [Fact]
    public void NextSequence_starts_at_one_when_no_codes_exist()
    {
        Assert.Equal(1, PublicReferenceCodes.NextSequence([], PublicReferenceCodes.OrderPrefix));
    }

    [Fact]
    public void NextSequence_is_max_plus_one_regardless_of_order()
    {
        var codes = new[] { "HAR-ORDER-0001", "HAR-ORDER-0007", "HAR-ORDER-0003" };

        Assert.Equal(8, PublicReferenceCodes.NextSequence(codes, PublicReferenceCodes.OrderPrefix));
    }

    [Fact]
    public void NextSequence_ignores_other_prefixes_legacy_and_non_numeric_codes()
    {
        var codes = new[] { "HAR-SERVICE-0050", "HAR-20260713-0003", "HAR-ORDER-ABCD", "HAR-ORDER-0002" };

        Assert.Equal(3, PublicReferenceCodes.NextSequence(codes, PublicReferenceCodes.OrderPrefix));
    }

    [Fact]
    public void NextSequence_matches_prefix_case_insensitively()
    {
        Assert.Equal(10, PublicReferenceCodes.NextSequence(["har-order-0009"], PublicReferenceCodes.OrderPrefix));
    }

    [Fact]
    public void NextSequence_continues_past_four_digits()
    {
        Assert.Equal(10000, PublicReferenceCodes.NextSequence(["HAR-ORDER-9999"], PublicReferenceCodes.OrderPrefix));
    }
}
