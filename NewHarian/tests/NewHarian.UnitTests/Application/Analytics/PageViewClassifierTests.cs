using NewHarian.Application.Analytics;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Analytics;

[Trait("Category", "Unit")]
public class PageViewClassifierTests
{
    [Theory]
    [InlineData("GET", 200, "/", null, "Home", "Index", null, PageKind.Home)]
    [InlineData("GET", 200, "/products", null, "Products", "Index", null, PageKind.ProductsBrowse)]
    [InlineData("GET", 200, "/products/hoa-chat/sp", null, "Products", "Detail", null, PageKind.ProductDetail)]
    [InlineData("GET", 200, "/services/dv/x", null, "Services", "Detail", null, PageKind.ServiceDetail)]
    [InlineData("GET", 200, "/cart", null, "Cart", "Index", null, PageKind.Cart)]
    [InlineData("GET", 200, "/checkout", null, "Checkout", "Index", null, PageKind.Checkout)]
    [InlineData("GET", 200, "/dealers/register", null, "Dealers", "Register", null, PageKind.Dealers)]
    [InlineData("GET", 200, "/contact", null, "Contact", "Index", null, PageKind.Contact)]
    [InlineData("GET", 200, "/about", null, "Home", "About", null, PageKind.OtherPublic)]
    public void Classify_maps_guest_html_gets(
        string method, int status, string path, string? accept,
        string controller, string action, string? area, PageKind expected)
    {
        Assert.Equal(expected, PageViewClassifier.Classify(method, status, path, accept, controller, action, area));
    }

    [Theory]
    [InlineData("POST", 200, "/", "Home", "Index")]
    [InlineData("GET", 404, "/products/x/y", "Products", "Detail")]
    [InlineData("GET", 302, "/products", "Products", "Index")]
    [InlineData("GET", 200, "/admin/Orders", "Orders", "Index")]
    [InlineData("GET", 200, "/api/variants/1", "Products", "Variant")]
    [InlineData("GET", 200, "/health", null, null)]
    public void Classify_skips_non_countable_requests(
        string method, int status, string path, string? controller, string? action)
    {
        var area = path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : null;
        Assert.Null(PageViewClassifier.Classify(method, status, path, null, controller, action, area));
    }

    [Fact]
    public void Classify_skips_json_only_accept()
    {
        Assert.Null(PageViewClassifier.Classify(
            "GET", 200, "/cart", "application/json", "Cart", "Index", null));
    }

    [Fact]
    public void Classify_keeps_browser_accept_with_html_and_json()
    {
        Assert.Equal(PageKind.Cart, PageViewClassifier.Classify(
            "GET", 200, "/cart", "text/html,application/xhtml+xml,application/json", "Cart", "Index", null));
    }

    [Theory]
    [InlineData(PageKind.Home, "Trang chủ")]
    [InlineData(PageKind.ProductDetail, "Chi tiết sản phẩm")]
    [InlineData(PageKind.OtherPublic, "Trang khác")]
    public void LabelVi_is_stable(PageKind kind, string label)
    {
        Assert.Equal(label, PageViewClassifier.LabelVi(kind));
    }
}
