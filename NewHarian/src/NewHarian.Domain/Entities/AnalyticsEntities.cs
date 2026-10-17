using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

/// <summary>Daily pageview rollup for one guest page kind (Asia/Ho_Chi_Minh date).</summary>
public class PageViewDaily
{
    public long Id { get; set; }
    public DateOnly Date { get; set; }
    public PageKind PageKind { get; set; }
    public long ViewCount { get; set; }
}

/// <summary>Daily product-detail view rollup (Asia/Ho_Chi_Minh date).</summary>
public class ProductViewDaily
{
    public long Id { get; set; }
    public DateOnly Date { get; set; }
    public int ProductId { get; set; }
    public long ViewCount { get; set; }

    public Product? Product { get; set; }
}
