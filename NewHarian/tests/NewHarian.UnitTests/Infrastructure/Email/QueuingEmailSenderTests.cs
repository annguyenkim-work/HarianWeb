using NewHarian.Infrastructure.Email;

namespace NewHarian.UnitTests.Infrastructure.Email;

[Trait("Category", "Unit")]
public class QueuingEmailSenderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("folder/")]
    public void SanitizeFileName_falls_back_when_no_file_name(string? name)
    {
        Assert.Equal("attachment.bin", QueuingEmailSender.SanitizeFileName(name));
    }

    [Theory]
    [InlineData("invoice.pdf", "invoice.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("reports/2026/HAR-ORDER-0001.xlsx", "HAR-ORDER-0001.xlsx")]
    public void SanitizeFileName_strips_directories(string name, string expected)
    {
        Assert.Equal(expected, QueuingEmailSender.SanitizeFileName(name));
    }

    [Fact]
    public void SanitizeFileName_replaces_invalid_chars_with_underscore()
    {
        Assert.Equal("a_b.pdf", QueuingEmailSender.SanitizeFileName("a" + '\0' + "b.pdf"));
    }

    [Theory]
    [InlineData("..\\..\\windows\\evil.exe")]
    [InlineData("/var/www/../secret.txt")]
    [InlineData("C:/temp/report.pdf")]
    public void SanitizeFileName_never_returns_a_path(string name)
    {
        var safe = QueuingEmailSender.SanitizeFileName(name);

        Assert.DoesNotContain(Path.DirectorySeparatorChar, safe);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, safe);
        Assert.Equal(-1, safe.IndexOfAny(Path.GetInvalidFileNameChars()));
    }
}
