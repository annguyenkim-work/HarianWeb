using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Analytics;
using NewHarian.Application.Catalog;
using NewHarian.Application.Formatting;
using NewHarian.Domain.Enums;
using NewHarian.Web.Models;

namespace NewHarian.Web.Controllers;

/// <summary>Guest hub for CatalogKind.Product. URLs under /products/…</summary>
public class ProductsController(ICatalogService catalog) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private const int PageSize = 12;

    [HttpGet("/products")]
    public async Task<IActionResult> Index(
        int page = 1,
        string? sort = null,
        string? tag = null,
        decimal? min = null,
        decimal? max = null,
        CancellationToken ct = default)
    {
        var criteria = ProductListCriteria.Normalize(page, sort, categorySlug: null, tag, min, max);
        return await ListingAsync(criteria, category: null, ct);
    }

    [HttpGet("/products/search")]
    public async Task<IActionResult> Search(string? q, string? tag, int page = 1, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var query = (q ?? "").Trim();
        var tagSlug = (tag ?? "").Trim();
        ViewData["Title"] = string.IsNullOrEmpty(query) ? "Search" : query;
        ViewBag.Q = query;
        ViewBag.Tag = tagSlug;
        ViewBag.Page = page;
        ViewBag.PageSize = PageSize;
        ViewBag.Tags = await catalog.GetPublishedProductTagsAsync(ct: ct);

        if (query.Length == 0 && tagSlug.Length == 0)
        {
            ViewBag.Total = 0;
            return View(Array.Empty<ProductCardDto>());
        }

        var (items, total) = await catalog.SearchProductsAsync(query, Lang, page, PageSize, tagSlug, ct);
        ViewBag.Total = total;
        return View(items);
    }

    public async Task<IActionResult> Category(
        string categorySlug,
        int page = 1,
        string? sort = null,
        string? tag = null,
        decimal? min = null,
        decimal? max = null,
        CancellationToken ct = default)
    {
        var cat = await catalog.GetCategoryAsync(categorySlug, Lang, ct);
        if (cat is null) return NotFound();

        // Category only has services → send users to /services/{slug} instead of empty products list.
        if (cat.PhysicalCount == 0 && cat.ServiceCount > 0)
            return RedirectToRoutePermanent("serviceCategory", new { categorySlug, page });

        var criteria = ProductListCriteria.Normalize(page, sort, categorySlug, tag, min, max);
        return await ListingAsync(criteria, cat, ct);
    }

    private async Task<IActionResult> ListingAsync(
        ProductListCriteria criteria, CategoryCardDto? category, CancellationToken ct)
    {
        var (items, total) = await catalog.ListProductsAsync(Lang, criteria, ct);
        var categories = await catalog.GetActiveCategoriesAsync(Lang, ct);
        var tags = await catalog.GetPublishedProductTagsAsync(category?.Slug, ct);
        var model = new ProductListingPageModel
        {
            Category = category,
            Products = items,
            Criteria = criteria,
            Total = total,
            Categories = categories.Where(c => c.PhysicalCount > 0).ToList(),
            Tags = tags
        };
        return View(category is null ? "Browse" : "Category", model);
    }

    public async Task<IActionResult> Detail(string categorySlug, string productSlug, CancellationToken ct)
    {
        var product = await catalog.GetProductAsync(categorySlug, productSlug, Lang, ct);
        if (product is not null)
        {
            HttpContext.Items[PageViewClassifier.ProductIdItemKey] = product.Id;
            return View(product);
        }

        // Not a physical product — maybe it's a service with the same slug.
        var service = await catalog.GetServiceAsync(categorySlug, productSlug, Lang, ct);
        if (service is not null)
            return RedirectToRoutePermanent("serviceDetail", new { categorySlug, productSlug });
        return NotFound();
    }

    [HttpGet("/api/variants/{id:int}")]
    public async Task<IActionResult> Variant(int id, string? kind, CancellationToken ct)
    {
        var catalogKind = string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase)
            ? CatalogKind.Service
            : CatalogKind.Product;
        var v = await catalog.GetVariantAsync(id, Lang, catalogKind, ct);
        if (v is null) return NotFound();
        return Json(new
        {
            id = v.Id,
            sku = v.Sku,
            label = v.Label,
            price = v.Price,
            priceText = MoneyFormat.Vnd(v.Price),
            colorMeaning = v.ColorMeaning,
            gallerySlideIndex = v.GallerySlideIndex
        });
    }

    /// <summary>Legacy /products/.../book → /services/.../book</summary>
    [HttpGet]
    public IActionResult Book(string categorySlug, string productSlug, int? variantId)
        => RedirectToRoutePermanent("serviceBook", new { categorySlug, productSlug, variantId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SubmitBook(string categorySlug, string productSlug)
        => RedirectToRoutePermanent("serviceBookSubmit", new { categorySlug, productSlug });

    [HttpGet]
    public IActionResult BookThanks(string categorySlug, string productSlug)
        => RedirectToRoutePermanent("serviceBookThanks", new { categorySlug, productSlug });
}
