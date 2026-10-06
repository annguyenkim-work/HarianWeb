using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class CategoriesController(IAdminCategoryService categories, IMediaStorage media) : Controller
{
    [HasPermission(Permissions.Categories.View)]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var (items, pager) = AdminPaging.Apply(await categories.ListCategoriesAsync(ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Categories.Edit)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
        {
            return PartialView("_CategoryForm", new CategorySaveRequest { IsActive = true });
        }

        var cat = await categories.GetCategoryAsync(id.Value, ct);
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
    [HasPermission(Permissions.Categories.Edit)]
    public async Task<IActionResult> Save(CategorySaveRequest model, IFormFile? imageFile, CancellationToken ct)
    {
        if (imageFile is { Length: > 0 })
        {
            await using var stream = imageFile.OpenReadStream();
            var uploaded = await media.SaveImageAsync(stream, imageFile.FileName, imageFile.ContentType, User.Identity?.Name, ct, "categories");
            model.ImageUrl = uploaded.Url;
        }

        var (ok, error, _) = await categories.SaveCategoryAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView("_CategoryForm", model);
        }
        return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Categories.Reorder)]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        await categories.MoveCategoryAsync(id, direction, ct);
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Categories.Edit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await categories.DeleteCategoryAsync(id, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã vô hiệu hóa danh mục." : error;
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
