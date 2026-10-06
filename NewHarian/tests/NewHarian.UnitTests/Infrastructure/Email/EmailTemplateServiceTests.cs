using NewHarian.Infrastructure.Email;

namespace NewHarian.UnitTests.Infrastructure.Email;

[Trait("Category", "Unit")]
public class EmailTemplateServiceTests
{
    private static readonly Dictionary<string, string?> Vars = new()
    {
        ["CustomerName"] = "An",
        ["OrderNumber"] = "HAR-ORDER-0001",
        ["Empty"] = null
    };

    [Theory]
    [InlineData("Xin chào {{CustomerName}}", "Xin chào An")]
    [InlineData("Đơn {{ OrderNumber }} / {{OrderNumber}}", "Đơn HAR-ORDER-0001 / HAR-ORDER-0001")]
    [InlineData("[{{Missing}}]", "[]")]
    [InlineData("[{{Empty}}]", "[]")]
    [InlineData("{CustomerName} {{bad-key}}", "{CustomerName} {{bad-key}}")]
    [InlineData("No placeholders", "No placeholders")]
    public void Apply_replaces_known_placeholders_and_blanks_unknown(string template, string expected)
    {
        Assert.Equal(expected, EmailTemplateService.Apply(template, Vars));
    }

    [Fact]
    public void Apply_does_not_expand_placeholders_inside_values()
    {
        var vars = new Dictionary<string, string?> { ["A"] = "{{B}}", ["B"] = "boom" };

        Assert.Equal("{{B}}", EmailTemplateService.Apply("{{A}}", vars));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("Tom & \"Jerry\" 'x'", "Tom &amp; &quot;Jerry&quot; &#39;x&#39;")]
    [InlineData("plain", "plain")]
    [InlineData(null, "")]
    public void Enc_html_encodes_values(string? value, string expected)
    {
        Assert.Equal(expected, EmailTemplateService.Enc(value));
    }
}
