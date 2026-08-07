using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Web.Areas.Admin.Services;

namespace NewHarian.Web.Areas.Admin.Controllers;

/// <summary>Admin CRUD for physical goods — CatalogKind.Product.</summary>
[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class ProductsController(
    IAdminCatalogService catalog,
    IMediaStorage media,
    AppDbContext db,
    IProductPreviewStore previewStore) : Controller
{
    private const CatalogKind Type = CatalogKind.Product;

    public async Task<IActionResult> Index(int? categoryId, int page = 1, CancellationToken ct = default)
    {
        ViewBag.ManagedType = Type;
        ViewBag.ListTitle = "Sản phẩm";
        ViewBag.AdminController = "Products";
        ViewBag.Categories = await catalog.GetCategoryOptionsAsync(ct);
        ViewBag.CategoryId = categoryId;
        var all = await catalog.ListProductsAsync(categoryId, Type, ct);
        var (items, pager) = AdminPaging.Apply(all, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        ViewBag.LockedProductType = Type;
        ViewBag.AdminController = "Products";
        ViewBag.Categories = await catalog.GetCategoryOptionsAsync(ct);
        ViewBag.Colors = await catalog.GetColorDefinitionsAsync(ct);

        if (id is null)
        {
            return PartialView("_ProductForm", new ProductSaveRequest
            {
                Status = ProductStatus.Draft,
                Kind = Type,
                HidePrice = false,
                Variants =
                [
                    new VariantSaveRequest { Sku = "", VariantLabel = "", ColorDefinitionId = null, Price = 0, IsDefault = true, IsActive = true, SortOrder = 1 }
                ]
            });
        }

        var p = await catalog.GetProductAsync(id.Value, Type, ct);
        if (p is null) return NotFound();
        return PartialView("_ProductForm", AdminProductFormHelper.ToSaveRequest(p));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProductSaveRequest model, CancellationToken ct)
    {
        ViewBag.LockedProductType = Type;
        ViewBag.AdminController = "Products";
        ViewBag.Categories = await catalog.GetCategoryOptionsAsync(ct);
        ViewBag.Colors = await catalog.GetColorDefinitionsAsync(ct);
        model.Variants ??= [];
        model.Kind = Type;
        model.HidePrice = false;

        if (model.Id is int existingId)
        {
            var existing = await catalog.GetProductAsync(existingId, Type, ct);
            if (existing is null)
            {
                ModelState.AddModelError(string.Empty, "Không thuộc danh sách sản phẩm (Physical).");
                return PartialView("_ProductForm", model);
            }
        }

        var (ok, error, _) = await catalog.SaveProductAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView("_ProductForm", model);
        }
        return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(ProductSaveRequest model, CancellationToken ct)
    {
        model.Variants ??= [];
        model.Kind = Type;
        model.HidePrice = false;

        if (model.CategoryId <= 0)
            return BadRequest(new { ok = false, error = "Chọn danh mục trước khi xem trước." });
        if (string.IsNullOrWhiteSpace(model.NameVi))
            return BadRequest(new { ok = false, error = "Nhập tên tiếng Việt để xem trước." });

        var category = await db.Categories.AsNoTracking()
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == model.CategoryId, ct);
        if (category is null)
            return BadRequest(new { ok = false, error = "Danh mục không hợp lệ." });

        await AdminProductFormHelper.ResolvePreviewImageUrlsAsync(db, model, ct);

        var colorIds = model.Variants
            .Where(v => v.ColorDefinitionId.HasValue)
            .Select(v => v.ColorDefinitionId!.Value)
            .Distinct()
            .ToList();
        var colors = new Dictionary<int, IReadOnlyList<ColorTranslationSnapshot>>();
        if (colorIds.Count > 0)
        {
            var colorEntities = await db.ColorDefinitions.AsNoTracking()
                .Include(c => c.Translations)
                .Where(c => colorIds.Contains(c.Id))
                .ToListAsync(ct);
            foreach (var c in colorEntities)
            {
                colors[c.Id] = c.Translations
                    .Select(t => new ColorTranslationSnapshot(t.LanguageCode, t.Name, t.Meaning))
                    .ToList();
            }
        }

        var categoryNames = category.Translations
            .GroupBy(t => t.LanguageCode)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var token = previewStore.Save(new ProductPreviewSnapshot
        {
            Request = model,
            CategorySlug = category.Slug,
            CategoryNames = categoryNames,
            Colors = colors
        });
        var url = Url.Action(nameof(PreviewView), new { token, lang = "vi", area = "Admin" });
        return Json(new { ok = true, url });
    }

    [HttpGet]
    public IActionResult PreviewView(string token, string? lang)
    {
        var snapshot = previewStore.Get(token);
        if (snapshot is null)
        {
            ViewData["Title"] = "Preview hết hạn";
            return View("PreviewExpired");
        }

        lang = lang is "en" or "ja" ? lang : "vi";
        ViewBag.IsPreview = true;
        ViewBag.PreviewToken = token;
        ViewBag.PreviewLang = lang;
        return View(ProductPreviewMapper.ToDetail(snapshot, lang));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { ok = false, error = "Chưa chọn file." });
        if (file.Length > MediaUploadLimits.MaxFileBytes)
            return BadRequest(new { ok = false, error = $"Ảnh tối đa {MediaUploadLimits.MaxFileLabel}." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await media.SaveImageAsync(stream, file.FileName, file.ContentType, User.Identity?.Name, ct);
            return Json(new { ok = true, id = result.Id, url = result.Url, fileName = result.FileName });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, int? categoryId, CancellationToken ct)
    {
        var p = await catalog.GetProductAsync(id, Type, ct);
        if (p is null) return NotFound();
        await catalog.MoveProductAsync(id, direction, Type, ct);
        return AdminListRedirect.ToRefererOrIndex(this, new { area = "Admin", categoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var p = await catalog.GetProductAsync(id, Type, ct);
        if (p is null)
            return Json(new { ok = false, error = "Không tìm thấy." });
        var (ok, error) = await catalog.DeleteProductAsync(id, Type, ct);
        return Json(new { ok, error });
    }
}
