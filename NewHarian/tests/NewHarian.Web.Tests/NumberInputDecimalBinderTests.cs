using NewHarian.Web.ModelBinding;

namespace NewHarian.Web.Tests;

[Trait("Category", "Unit")]
public class NumberInputDecimalBinderTests
{
    [Theory]
    [InlineData("22.5", "22.5")]
    [InlineData(" 22.55 ", "22.55")]
    [InlineData("-0.5", "-0.5")]
    [InlineData("100.00", "100")]
    public void Number_input_values_parse_invariantly(string raw, string expected)
    {
        Assert.True(NumberInputDecimalBinder.TryParseNumberInput(raw, out var value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("25")]
    [InlineData("100.000")]
    [InlineData("22,5")]
    [InlineData("1.234.567")]
    [InlineData("abc")]
    public void Other_shapes_fall_back_to_culture_binder(string? raw)
    {
        Assert.False(NumberInputDecimalBinder.TryParseNumberInput(raw, out _));
    }
}
