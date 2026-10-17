using NewHarian.Domain.Enums;

namespace NewHarian.Application.Analytics;

/// <summary>Pure path / route rules for which guest GET responses count as a pageview.</summary>
public static class PageViewClassifier
{
    public const string ProductIdItemKey = "Analytics.ProductId";

    private static readonly string[] SkipPrefixes =
    [
        "/admin", "/api", "/health", "/lib", "/css", "/js", "/uploads",
        "/_framework", "/favicon", "/swagger", "/hangfire",
    ];

    /// <summary>Returns null when the request must not be recorded.</summary>
    public static PageKind? Classify(string method, int statusCode, string path, string? accept, string? controller, string? action, string? area)
    {
        if (!HttpMethodsEqual(method, "GET")) return null;
        if (statusCode is < 200 or >= 300) return null;
        if (!string.IsNullOrEmpty(area) && area.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return null;
        if (WantsJsonOnly(accept)) return null;

        var p = NormalizePath(path);
        if (p.Length == 0) return PageKind.Home;
        foreach (var prefix in SkipPrefixes)
        {
            if (p.Equals(prefix.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(prefix.TrimStart('/') + "/", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        if (Match(controller, "Home") && Match(action, "Index")) return PageKind.Home;
        if (Match(controller, "Home") && Match(action, "StatusCodePage")) return null;

        if (Match(controller, "Products"))
        {
            if (Match(action, "Detail")) return PageKind.ProductDetail;
            if (Match(action, "Book") || Match(action, "BookThanks") || Match(action, "SubmitBook")) return PageKind.OtherPublic;
            return PageKind.ProductsBrowse;
        }

        if (Match(controller, "Services"))
        {
            if (Match(action, "Detail")) return PageKind.ServiceDetail;
            if (Match(action, "Book") || Match(action, "BookThanks") || Match(action, "SubmitBook")) return PageKind.OtherPublic;
            return PageKind.ServicesBrowse;
        }

        if (Match(controller, "Cart")) return PageKind.Cart;
        if (Match(controller, "Checkout")) return PageKind.Checkout;
        if (Match(controller, "Dealers")) return PageKind.Dealers;
        if (Match(controller, "Contact")) return PageKind.Contact;
        if (Match(controller, "Careers") || Match(controller, "Jobs")) return PageKind.Careers;
        if (Match(controller, "News") || Match(controller, "Posts") || Match(controller, "Blog")) return PageKind.News;

        // Fallback by path when endpoint metadata is missing (e.g. unit tests).
        if (p is "" or "home" or "home/index") return PageKind.Home;
        if (p.StartsWith("products/", StringComparison.OrdinalIgnoreCase) || p.Equals("products", StringComparison.OrdinalIgnoreCase))
        {
            var segments = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 3 ? PageKind.ProductDetail : PageKind.ProductsBrowse;
        }
        if (p.StartsWith("services/", StringComparison.OrdinalIgnoreCase) || p.Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            var segments = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 3 && !segments.Contains("book", StringComparer.OrdinalIgnoreCase)
                ? PageKind.ServiceDetail
                : segments.Length >= 3 && segments.Contains("book", StringComparer.OrdinalIgnoreCase)
                    ? PageKind.OtherPublic
                    : PageKind.ServicesBrowse;
        }
        if (p.StartsWith("cart", StringComparison.OrdinalIgnoreCase)) return PageKind.Cart;
        if (p.StartsWith("checkout", StringComparison.OrdinalIgnoreCase)) return PageKind.Checkout;
        if (p.StartsWith("dealers", StringComparison.OrdinalIgnoreCase)) return PageKind.Dealers;
        if (p.StartsWith("contact", StringComparison.OrdinalIgnoreCase)) return PageKind.Contact;
        if (p.StartsWith("careers", StringComparison.OrdinalIgnoreCase) || p.StartsWith("jobs", StringComparison.OrdinalIgnoreCase))
            return PageKind.Careers;
        if (p.StartsWith("news", StringComparison.OrdinalIgnoreCase) || p.StartsWith("blog", StringComparison.OrdinalIgnoreCase))
            return PageKind.News;

        return PageKind.OtherPublic;
    }

    public static string LabelVi(PageKind kind) => kind switch
    {
        PageKind.Home => "Trang chủ",
        PageKind.ProductsBrowse => "Danh sách sản phẩm",
        PageKind.ProductDetail => "Chi tiết sản phẩm",
        PageKind.ServicesBrowse => "Danh sách dịch vụ",
        PageKind.ServiceDetail => "Chi tiết dịch vụ",
        PageKind.News => "Tin tức",
        PageKind.Cart => "Giỏ hàng",
        PageKind.Checkout => "Thanh toán",
        PageKind.Dealers => "Đại lý",
        PageKind.Contact => "Liên hệ",
        PageKind.Careers => "Tuyển dụng",
        PageKind.OtherPublic => "Trang khác",
        _ => kind.ToString(),
    };

    private static bool WantsJsonOnly(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept)) return false;
        var wantsJson = accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        var wantsHtml = accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        return wantsJson && !wantsHtml;
    }

    private static string NormalizePath(string path)
    {
        var p = (path ?? "/").Trim();
        if (p.Length == 0 || p == "/") return "";
        return p.TrimStart('/').TrimEnd('/');
    }

    private static bool Match(string? value, string expected)
        => !string.IsNullOrEmpty(value) && value.Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static bool HttpMethodsEqual(string method, string expected)
        => method.Equals(expected, StringComparison.OrdinalIgnoreCase);
}
