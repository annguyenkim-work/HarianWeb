using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class CategoriesController(IAdminCatalogService catalog, IMediaStorage media) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var (items, pager) = AdminPaging.Apply(await catalog.ListCategoriesAsync(ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
        {
            return PartialView("_CategoryForm", new CategorySaveRequest { IsActive = true });
        }

        var cat = await catalog.GetCategoryAsync(id.Value, ct);
        if (cat is null) return NotFound();
        return PartialView("_CategoryForm", new CategorySaveRequest
        {
            Id = cat.Id,
            Slug = cat.Slug,
            SortOrder = cat.SortOrder,
            IsActive = cat.IsActive,
            ShowOnHome = cat.ShowOnHome,
            ImageUrl = cat.ImageUrl,
            NameVi = cat.NameVi,
            DescVi = cat.DescVi,
            NameEn = cat.NameEn,
            DescEn = cat.DescEn,
            NameJa = cat.NameJa,
            DescJa = cat.DescJa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CategorySaveRequest model, IFormFile? imageFile, CancellationToken ct)
    {
        if (imageFile is { Length: > 0 })
        {
            await using var stream = imageFile.OpenReadStream();
            var uploaded = await media.SaveImageAsync(stream, imageFile.FileName, imageFile.ContentType, User.Identity?.Name, ct, "categories");
            model.ImageUrl = uploaded.Url;
        }

        var (ok, error, _) = await catalog.SaveCategoryAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView("_CategoryForm", model);
        }
        return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        await catalog.MoveCategoryAsync(id, direction, ct);
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await catalog.DeleteCategoryAsync(id, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã vô hiệu hóa danh mục." : error;
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
