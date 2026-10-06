using NewHarian.Application.Address;
using NewHarian.Infrastructure.Address;

namespace NewHarian.UnitTests.Application.Address;

[Trait("Category", "Unit")]
public class AddressFormatTests
{
    private const string LineError = "Vui lòng điền địa chỉ (số nhà, đường) từ 5-500 ký tự.";
    private const string DivisionError = "Vui lòng chọn Tỉnh/Thành phố và Xã/Phường.";

    private static readonly Lazy<VietnamDivisionCatalog> SharedCatalog = new(() => new VietnamDivisionCatalog());
    private static VietnamDivisionCatalog Catalog => SharedCatalog.Value;

    private static (VietnamProvinceDto Province, VietnamCommuneDto Commune) Hanoi()
    {
        var province = Catalog.Provinces.Single(p => p.Code == "01");
        return (province, Catalog.CommunesFor(province.Code)[0]);
    }

    [Theory]
    [InlineData("12 Lê Lợi", "Phường Bến Nghé", "TP Hồ Chí Minh", "12 Lê Lợi, Phường Bến Nghé, TP Hồ Chí Minh")]
    [InlineData("12 Lê Lợi", null, "Hà Nội", "12 Lê Lợi, Hà Nội")]
    [InlineData("  ", "Xã A", "", "Xã A")]
    [InlineData(null, null, null, "")]
    public void Join_skips_blank_parts(string? line, string? commune, string? province, string expected)
    {
        Assert.Equal(expected, AddressFormat.Join(line, commune, province));
    }

    [Fact]
    public void Catalog_loads_embedded_divisions()
    {
        var (province, commune) = Hanoi();

        Assert.Equal("Thành phố Hà Nội", province.Name);
        Assert.Equal("01", commune.ProvinceCode);
        Assert.Empty(Catalog.CommunesFor("not-a-province"));
    }

    [Fact]
    public void Require_returns_null_for_valid_address()
    {
        var (province, commune) = Hanoi();

        Assert.Null(AddressFormat.Require(Catalog, province.Code, commune.Code, "12 Hàng Bài"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("   12   ")]
    public void Require_rejects_short_street_line_before_checking_division(string? line)
    {
        Assert.Equal(LineError, AddressFormat.Require(Catalog, "invalid", "invalid", line));
    }

    [Fact]
    public void Require_rejects_street_line_over_500_chars()
    {
        var (province, commune) = Hanoi();

        Assert.Equal(LineError, AddressFormat.Require(Catalog, province.Code, commune.Code, new string('a', 501)));
    }

    [Fact]
    public void Require_rejects_unknown_province_or_commune_from_other_province()
    {
        var (province, commune) = Hanoi();
        var otherProvince = Catalog.Provinces.First(p => p.Code != province.Code);

        Assert.Equal(DivisionError, AddressFormat.Require(Catalog, "99999", commune.Code, "12 Hàng Bài"));
        Assert.Equal(DivisionError, AddressFormat.Require(Catalog, province.Code, null, "12 Hàng Bài"));
        Assert.Equal(DivisionError, AddressFormat.Require(Catalog, otherProvince.Code, commune.Code, "12 Hàng Bài"));
    }

    [Fact]
    public void TryBind_resolves_names_for_valid_codes()
    {
        var (province, commune) = Hanoi();

        var ok = AddressFormat.TryBind(Catalog, $" {province.Code} ", commune.Code, "12 Hàng Bài", out var resolved, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(new ResolvedVietnamAddress(province.Code, province.Name, commune.Code, commune.Name), resolved);
    }

    [Fact]
    public void TryBind_returns_error_and_default_address_when_invalid()
    {
        var ok = AddressFormat.TryBind(Catalog, "99999", "1", "12 Hàng Bài", out var resolved, out var error);

        Assert.False(ok);
        Assert.Equal(DivisionError, error);
        Assert.Equal(default, resolved);
    }
}
