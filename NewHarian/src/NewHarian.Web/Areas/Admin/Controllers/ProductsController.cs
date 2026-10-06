using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;
using NewHarian.Web.Areas.Admin.Services;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

/// <summary>Admin CRUD for physical goods — CatalogKind.Product.</summary>
[Area("Admin")]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class ProductsController(
    IAdminProductService products,
    IAdminCategoryService categories,
    IAdminColorService colors,
    IMediaStorage media,
    IAdminProductPreviewBuilder previewBuilder,
    IProductPreviewStore previewStore) : Controller
{
    private const CatalogKind Type = CatalogKind.Product;

    [HttpGet]
    [HasPermission(Permissions.Products.SuggestVariants)]
    public async Task<IActionResult> SuggestVariants(string? q, CancellationToken ct)
        => Json(await products.SuggestVariantsAsync(q, 15, ct));

    [HasPermission(Permissions.Products.View)]
    public async Task<IActionResult> Index(int? categoryId, int page = 1, CancellationToken ct = default)
    {
        ViewBag.ManagedType = Type;
        ViewBag.ListTitle = "Sản phẩm";
        ViewBag.AdminController = "Products";
        ViewBag.Categories = await categories.GetCategoryOptionsAsync(ct);
        ViewBag.CategoryId = categoryId;
        var all = await products.ListProductsAsync(categoryId, ct);
        var (items, pager) = AdminPaging.Apply(all, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Products.Edit)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        ViewBag.LockedProductType = Type;
        ViewBag.AdminController = "Products";
        ViewBag.Categories = await categories.GetCategoryOptionsAsync(ct);
        ViewBag.Colors = await colors.GetOptionsAsync(ct);

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

        var p = await products.GetProductAsync(id.Value, ct);
        if (p is null) return NotFound();
        return PartialView("_ProductForm", AdminProductFormHelper.ToSaveRequest(p));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Products.Edit)]
    public async Task<IActionResult> Save(ProductSaveRequest model, CancellationToken ct)
    {
        ViewBag.LockedProductType = Type;
        ViewBag.AdminController = "Products";
        ViewBag.Categories = await categories.GetCategoryOptionsAsync(ct);
        ViewBag.Colors = await colors.GetOptionsAsync(ct);
        model.Variants ??= [];
        model.Kind = Type;
        model.HidePrice = false;

        if (model.Id is int existingId)
        {
            var existing = await products.GetProductAsync(existingId, ct);
            if (existing is null)
            {
                ModelState.AddModelError(string.Empty, "Không thuộc danh sách sản phẩm (Physical).");
                return PartialView("_ProductForm", model);
            }
        }

        var (ok, error, _) = await products.SaveProductAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView("_ProductForm", model);
        }
        return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Products.Preview)]
    public async Task<IActionResult> Preview(ProductSaveRequest model, CancellationToken ct)
    {
        model.Variants ??= [];
        model.Kind = Type;
        model.HidePrice = false;

        var (ok, error, snapshot) = await previewBuilder.BuildAsync(model, ct);
        if (!ok || snapshot is null)
            return BadRequest(new { ok = false, error = error ?? "Không tạo được preview." });

        var token = previewStore.Save(snapshot);
        var url = Url.Action(nameof(PreviewView), new { token, lang = "vi", area = "Admin" });
        return Json(new { ok = true, url });
    }

    [HttpGet]
    [HasPermission(Permissions.Products.Preview)]
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
    [HasPermission(Permissions.Products.UploadImage)]
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
    [HasPermission(Permissions.Products.Reorder)]
    public async Task<IActionResult> Move(int id, int direction, int? categoryId, CancellationToken ct)
    {
        var p = await products.GetProductAsync(id, ct);
        if (p is null) return NotFound();
        await products.MoveProductAsync(id, direction, ct);
        return AdminListRedirect.ToRefererOrIndex(this, new { area = "Admin", categoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Products.Edit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var p = await products.GetProductAsync(id, ct);
        if (p is null)
            return Json(new { ok = false, error = "Không tìm thấy." });
        var (ok, error) = await products.DeleteProductAsync(id, ct);
        return Json(new { ok, error });
    }
}
