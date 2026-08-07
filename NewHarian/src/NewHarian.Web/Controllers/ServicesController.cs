using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Controllers;

/// <summary>Guest hub for CatalogKind.Service. URLs under /services/…</summary>
public class ServicesController(ICatalogService catalog, IServiceBookingService bookings) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private const int PageSize = 12;

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await catalog.GetProductsByTypeAsync(CatalogKind.Service, Lang, page, PageSize, ct);
        ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.PageSize = PageSize;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Category(string categorySlug, int page = 1, CancellationToken ct = default)
    {
        var cat = await catalog.GetCategoryAsync(categorySlug, Lang, ct);
        if (cat is null) return NotFound();

        if (cat.ServiceCount == 0 && cat.PhysicalCount > 0)
            return RedirectToRoutePermanent("productCategory", new { categorySlug, page });

        page = Math.Max(1, page);
        var (items, total) = await catalog.GetProductsByCategoryAsync(
            categorySlug, Lang, page, PageSize, CatalogKind.Service, ct);
        ViewBag.Category = cat;
        ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.PageSize = PageSize;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(string categorySlug, string productSlug, CancellationToken ct)
    {
        var product = await catalog.GetServiceAsync(categorySlug, productSlug, Lang, ct);
        if (product is not null) return View(product);

        // Not a service — maybe it's a physical product with the same slug.
        var physical = await catalog.GetProductAsync(categorySlug, productSlug, Lang, ct);
        if (physical is not null)
            return RedirectToRoutePermanent("productDetail", new { categorySlug, productSlug });
        return NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Book(string categorySlug, string productSlug, int? variantId, CancellationToken ct)
    {
        var product = await catalog.GetServiceAsync(categorySlug, productSlug, Lang, ct);
        if (product is null) return NotFound();

        var selected = variantId.HasValue
            ? product.Variants.FirstOrDefault(v => v.Id == variantId)
            : product.Variants.FirstOrDefault(v => v.IsDefault) ?? product.Variants.FirstOrDefault();

        ViewBag.Product = product;
        ViewBag.SelectedVariantId = selected?.Id ?? 0;
        return View(new ServiceBookingRequest
        {
            ServiceVariantId = selected?.Id ?? 0,
            PreferredDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            PreferredTime = "Sáng",
            LanguageCode = Lang
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("booking-submit")]
    public async Task<IActionResult> SubmitBook(string categorySlug, string productSlug, ServiceBookingRequest model, CancellationToken ct)
    {
        var product = await catalog.GetServiceAsync(categorySlug, productSlug, Lang, ct);
        if (product is null) return NotFound();

        model.LanguageCode = Lang;
        var (ok, error, bookingNumber) = await bookings.CreateAsync(model, ct);
        if (!ok)
        {
            ViewBag.Product = product;
            ViewBag.SelectedVariantId = model.ServiceVariantId;
            ModelState.AddModelError(string.Empty, error ?? "Không gửi được.");
            return View("Book", model);
        }

        TempData["BookingNumber"] = bookingNumber;
        return RedirectToAction(nameof(BookThanks), new { categorySlug, productSlug });
    }

    [HttpGet]
    public async Task<IActionResult> BookThanks(string categorySlug, string productSlug, CancellationToken ct)
    {
        var product = await catalog.GetServiceAsync(categorySlug, productSlug, Lang, ct);
        if (product is null) return NotFound();
        ViewBag.BookingNumber = TempData["BookingNumber"];
        return View(product);
    }
}
