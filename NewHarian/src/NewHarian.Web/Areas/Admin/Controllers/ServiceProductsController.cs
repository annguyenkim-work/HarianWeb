using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;
using NewHarian.Web.Areas.Admin.Services;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin CRUD for services — CatalogKind.Service.
/// Separate Services table; separate controller/page for clarity.
/// Reuses Products form/list views.
/// </summary>
[Area("Admin")]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class ServiceProductsController(
    IAdminServiceOfferingService services,
    IAdminCategoryService categories,
    IAdminColorService colors,
    IMediaStorage media,
    IAdminProductPreviewBuilder previewBuilder,
    IProductPreviewStore previewStore) : Controller
{
    private const CatalogKind Type = CatalogKind.Service;
    private const string SharedIndex = "~/Areas/Admin/Views/Products/Index.cshtml";
    private const string SharedForm = "~/Areas/Admin/Views/Products/_ProductForm.cshtml";
    private const string SharedPreview = "~/Areas/Admin/Views/Products/PreviewView.cshtml";
    private const string SharedPreviewExpired = "~/Areas/Admin/Views/Products/PreviewExpired.cshtml";

    [HasPermission(Permissions.Services.View)]
    public async Task<IActionResult> Index(int? categoryId, int page = 1, CancellationToken ct = default)
    {
        ViewBag.ManagedType = Type;
        ViewBag.ListTitle = "Dịch vụ";
        ViewBag.AdminController = "ServiceProducts";
        ViewBag.Categories = await categories.GetCategoryOptionsAsync(ct);
        ViewBag.CategoryId = categoryId;
        var all = await services.ListServicesAsync(categoryId, ct);
        var (items, pager) = AdminPaging.Apply(all, page);
        ViewBag.Pager = pager;
        return View(SharedIndex, items);
    }

    [HttpGet]
    [HasPermission(Permissions.Services.Edit)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        ViewBag.LockedProductType = Type;
        ViewBag.AdminController = "ServiceProducts";
        ViewBag.Categories = await categories.GetCategoryOptionsAsync(ct);
        ViewBag.Colors = await colors.GetOptionsAsync(ct);

        if (id is null)
        {
            return PartialView(SharedForm, new ProductSaveRequest
            {
                Status = ProductStatus.Draft,
                Kind = Type,
                HidePrice = true,
                Variants =
                [
                    new VariantSaveRequest { Sku = "", VariantLabel = "", ColorDefinitionId = null, Price = 0, IsDefault = true, IsActive = true, SortOrder = 1 }
                ]
            });
        }

        var p = await services.GetServiceAsync(id.Value, ct);
        if (p is null) return NotFound();
        return PartialView(SharedForm, AdminProductFormHelper.ToSaveRequest(p));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Services.Edit)]
    public async Task<IActionResult> Save(ProductSaveRequest model, CancellationToken ct)
    {
        ViewBag.LockedProductType = Type;
        ViewBag.AdminController = "ServiceProducts";
        ViewBag.Categories = await categories.GetCategoryOptionsAsync(ct);
        ViewBag.Colors = await colors.GetOptionsAsync(ct);
        model.Variants ??= [];
        model.Kind = Type;

        if (model.Id is int existingId)
        {
            var existing = await services.GetServiceAsync(existingId, ct);
            if (existing is null)
            {
                ModelState.AddModelError(string.Empty, "Không thuộc danh sách dịch vụ (Service).");
                return PartialView(SharedForm, model);
            }
        }

        var (ok, error, _) = await services.SaveServiceAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView(SharedForm, model);
        }
        return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Services.Preview)]
    public async Task<IActionResult> Preview(ProductSaveRequest model, CancellationToken ct)
    {
        model.Variants ??= [];
        model.Kind = Type;

        var (ok, error, snapshot) = await previewBuilder.BuildAsync(model, ct);
        if (!ok || snapshot is null)
            return BadRequest(new { ok = false, error = error ?? "Không tạo được preview." });

        var token = previewStore.Save(snapshot);
        var url = Url.Action(nameof(PreviewView), new { token, lang = "vi", area = "Admin" });
        return Json(new { ok = true, url });
    }

    [HttpGet]
    [HasPermission(Permissions.Services.Preview)]
    public IActionResult PreviewView(string token, string? lang)
    {
        var snapshot = previewStore.Get(token);
        if (snapshot is null)
        {
            ViewData["Title"] = "Preview hết hạn";
            return View(SharedPreviewExpired);
        }

        lang = lang is "en" or "ja" ? lang : "vi";
        ViewBag.IsPreview = true;
        ViewBag.PreviewToken = token;
        ViewBag.PreviewLang = lang;
        return View(SharedPreview, ProductPreviewMapper.ToDetail(snapshot, lang));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Services.UploadImage)]
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
    [HasPermission(Permissions.Services.Reorder)]
    public async Task<IActionResult> Move(int id, int direction, int? categoryId, CancellationToken ct)
    {
        var p = await services.GetServiceAsync(id, ct);
        if (p is null) return NotFound();
        await services.MoveServiceAsync(id, direction, ct);
        return AdminListRedirect.ToRefererOrIndex(this, new { area = "Admin", categoryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Services.Edit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var p = await services.GetServiceAsync(id, ct);
        if (p is null)
            return Json(new { ok = false, error = "Không tìm thấy." });
        var (ok, error) = await services.DeleteServiceAsync(id, ct);
        return Json(new { ok, error });
    }
}
