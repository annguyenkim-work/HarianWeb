using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Posts;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class PostsController(IAdminSitePostService posts, IMediaStorage media) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(PostKind kind = PostKind.News, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Kind = kind;
        ViewData["Title"] = kind == PostKind.Job ? "Tin tuyển dụng" : "Tin tức";
        var (items, pager) = AdminPaging.Apply(await posts.ListAsync(kind, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(PostKind kind, int? id, CancellationToken ct)
    {
        ViewBag.Kind = kind;
        ViewData["Title"] = id is null
            ? (kind == PostKind.Job ? "Thêm tin tuyển dụng" : "Thêm tin tức")
            : (kind == PostKind.Job ? "Sửa tin tuyển dụng" : "Sửa tin tức");

        if (id is null)
        {
            return View(new SitePostSaveRequest { Kind = kind, IsPublished = false });
        }

        var p = await posts.GetAsync(id.Value, ct);
        if (p is null || p.Kind != kind) return NotFound();
        return View(new SitePostSaveRequest
        {
            Id = p.Id,
            Kind = p.Kind,
            Slug = p.Slug,
            IsPublished = p.IsPublished,
            SortOrder = p.SortOrder,
            CoverImageMediaFileId = p.CoverImageMediaFileId,
            CoverImageUrl = p.CoverImageUrl,
            TitleVi = p.TitleVi,
            ExcerptVi = p.ExcerptVi,
            BodyVi = p.BodyVi,
            TitleEn = p.TitleEn,
            ExcerptEn = p.ExcerptEn,
            BodyEn = p.BodyEn,
            TitleJa = p.TitleJa,
            ExcerptJa = p.ExcerptJa,
            BodyJa = p.BodyJa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SitePostSaveRequest model, IFormFile? coverFile, CancellationToken ct)
    {
        ViewBag.Kind = model.Kind;
        ViewData["Title"] = model.Id is null
            ? (model.Kind == PostKind.Job ? "Thêm tin tuyển dụng" : "Thêm tin tức")
            : (model.Kind == PostKind.Job ? "Sửa tin tuyển dụng" : "Sửa tin tức");

        if (coverFile is { Length: > 0 })
        {
            await using var stream = coverFile.OpenReadStream();
            var up = await media.SaveImageAsync(stream, coverFile.FileName, coverFile.ContentType, User.Identity?.Name, ct, "posts");
            model.CoverImageMediaFileId = up.Id;
            model.CoverImageUrl = up.Url;
        }

        var (ok, error, _) = await posts.SaveAsync(model, ct);
        if (!ok)
        {
            TempData["Error"] = error;
            return View(model);
        }
        TempData["Success"] = "Đã lưu.";
        return RedirectToAction(nameof(Index), new { kind = model.Kind, area = "Admin" });
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
            var result = await media.SaveImageAsync(stream, file.FileName, file.ContentType, User.Identity?.Name, ct, "posts");
            return Json(new { ok = true, id = result.Id, url = result.Url, fileName = result.FileName });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, PostKind kind, CancellationToken ct)
    {
        await posts.MoveAsync(id, direction, ct);
        return AdminListRedirect.ToRefererOrIndex(this, new { kind, area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, PostKind kind, CancellationToken ct)
    {
        var (ok, error) = await posts.DeleteAsync(id, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xóa." : error;
        return AdminListRedirect.ToRefererOrIndex(this, new { kind, area = "Admin" });
    }
}
