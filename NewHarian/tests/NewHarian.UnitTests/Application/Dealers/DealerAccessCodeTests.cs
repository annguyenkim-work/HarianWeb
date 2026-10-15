using NewHarian.Application.Dealers;

namespace NewHarian.UnitTests.Application.Dealers;

[Trait("Category", "Unit")]
public class DealerAccessCodeTests
{
    [Fact]
    public void Generate_returns_12_chars_from_crockford_alphabet()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = DealerAccessCode.Generate();
            Assert.Equal(DealerAccessCode.Length, code.Length);
            Assert.All(code, c => Assert.Contains(c, DealerAccessCode.Alphabet));
        }
    }

    [Fact]
    public void Generate_does_not_repeat()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => DealerAccessCode.Generate()).ToHashSet();
        Assert.Equal(2000, codes.Count);
    }

    [Fact]
    public void Alphabet_excludes_look_alike_letters()
    {
        Assert.Equal(32, DealerAccessCode.Alphabet.Length);
        foreach (var c in "ILOU")
            Assert.DoesNotContain(c, DealerAccessCode.Alphabet);
    }

    [Theory]
    [InlineData("HRN-7K3M-Q9TZ-4WXA", "7K3MQ9TZ4WXA")]
    [InlineData("hrn-7k3m-q9tz-4wxa", "7K3MQ9TZ4WXA")]
    [InlineData(" HRN 7K3M Q9TZ 4WXA ", "7K3MQ9TZ4WXA")]
    [InlineData("7K3MQ9TZ4WXA", "7K3MQ9TZ4WXA")]
    [InlineData("7k3m-q9tz-4wxa", "7K3MQ9TZ4WXA")]
    [InlineData("HRN-OK3M-Q9TZ-4WXA", "0K3MQ9TZ4WXA")]
    [InlineData("HRN-IK3M-Q9TZ-4WXL", "1K3MQ9TZ4WX1")]
    [InlineData("HRN-HRN1-2345-6789", "HRN123456789")]
    public void Normalize_accepts_case_spacing_prefix_and_look_alikes(string input, string expected)
    {
        Assert.Equal(expected, DealerAccessCode.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("HRN-7K3M-Q9TZ-4WX")]
    [InlineData("HRN-7K3M-Q9TZ-4WXAB")]
    [InlineData("HRN-7K3M-Q9TZ-4WU1")]
    [InlineData("HRN-7K3M-Q9TZ-4W*A")]
    [InlineData("' OR 1=1 --")]
    public void Normalize_rejects_malformed_input(string? input)
    {
        Assert.Null(DealerAccessCode.Normalize(input));
    }

    [Fact]
    public void Normalize_rejects_overlong_input_before_scanning()
    {
        Assert.Null(DealerAccessCode.Normalize(new string('-', 60) + "7K3MQ9TZ4WXA"));
    }

    [Fact]
    public void Format_last4_and_masked_round_trip()
    {
        var code = DealerAccessCode.Generate();
        var formatted = DealerAccessCode.Format(code);

        Assert.Matches(@"^HRN-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$", formatted);
        Assert.Equal(code, DealerAccessCode.Normalize(formatted));
        Assert.Equal(code[^4..], DealerAccessCode.Last4(code));
        Assert.Equal($"HRN-••••-••••-{code[^4..]}", DealerAccessCode.Masked(DealerAccessCode.Last4(code)));
    }
}
