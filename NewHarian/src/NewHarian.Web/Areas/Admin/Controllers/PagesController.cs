using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class PagesController(IAdminCmsService cms, IMediaStorage media) : Controller
{
    public async Task<IActionResult> Index(string? module, CancellationToken ct)
    {
        if (string.Equals(module, "careers", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Posts", new { kind = "Job", area = "Admin" });

        ViewBag.Module = module;
        var pages = await cms.ListPagesAsync(module, ct);
        // Careers content is managed as Job posts, not CMS page blocks.
        pages = pages.Where(p => !string.Equals(p.ModuleCode, "careers", StringComparison.OrdinalIgnoreCase)).ToList();
        return View(pages);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var page = await cms.GetPageAsync(id, ct);
        if (page is null) return NotFound();
        if (string.Equals(page.ModuleCode, "careers", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Posts", new { kind = "Job", area = "Admin" });
        return View(new CmsPageSaveRequest
        {
            Id = page.Id,
            IsPublished = page.IsPublished,
            TitleVi = page.TitleVi,
            MetaTitleVi = page.MetaTitleVi,
            MetaDescriptionVi = page.MetaDescriptionVi,
            TitleEn = page.TitleEn,
            MetaTitleEn = page.MetaTitleEn,
            MetaDescriptionEn = page.MetaDescriptionEn,
            TitleJa = page.TitleJa,
            MetaTitleJa = page.MetaTitleJa,
            MetaDescriptionJa = page.MetaDescriptionJa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CmsPageSaveRequest model, CancellationToken ct)
    {
        if (await IsCareersPageAsync(model.Id, ct))
            return RedirectToAction("Index", "Posts", new { kind = "Job", area = "Admin" });

        var (ok, error) = await cms.SavePageMetaAsync(model, ct);
        if (!ok)
        {
            TempData["Error"] = error;
            return View(model);
        }
        TempData["Success"] = "Đã lưu meta trang.";
        return RedirectToAction(nameof(Edit), new { id = model.Id, area = "Admin" });
    }

    [HttpGet]
    public async Task<IActionResult> Blocks(int id, CancellationToken ct)
    {
        var page = await cms.GetPageAsync(id, ct);
        if (page is null) return NotFound();
        if (string.Equals(page.ModuleCode, "careers", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Posts", new { kind = "Job", area = "Admin" });
        ViewBag.Page = page;
        return View(await cms.ListBlocksAsync(id, ct));
    }

    [HttpGet]
    public async Task<IActionResult> EditBlock(int pageId, int? id, ContentBlockType? type, CancellationToken ct)
    {
        if (!await PageExists(pageId, ct)) return NotFound();
        if (await IsCareersPageAsync(pageId, ct))
            return RedirectToAction("Index", "Posts", new { kind = "Job", area = "Admin" });
        ViewBag.PageId = pageId;

        if (id is null)
        {
            return View(new CmsBlockSaveRequest
            {
                PageId = pageId,
                BlockType = type ?? ContentBlockType.RichText,
                IsPublished = true,
                ImagePosition = "right",
                ExtraData = type == ContentBlockType.DataTable
                    ? """{"rows":[{"id":"row-1","sortOrder":1,"label":{"vi":"","ja":"","en":""},"value":{"vi":"","ja":"","en":""}}]}"""
                    : null
            });
        }

        var block = await cms.GetBlockAsync(id.Value, ct);
        if (block is null || block.PageId != pageId) return NotFound();
        return View(new CmsBlockSaveRequest
        {
            Id = block.Id,
            PageId = block.PageId,
            BlockType = block.BlockType,
            IsPublished = block.IsPublished,
            MediaFileId = block.MediaFileId,
            LinkUrl = block.LinkUrl,
            ImagePosition = block.ImagePosition,
            ExtraData = block.ExtraData,
            SpacingAfterRem = block.SpacingAfterRem,
            TitleVi = block.TitleVi,
            BodyVi = block.BodyVi,
            TitleEn = block.TitleEn,
            BodyEn = block.BodyEn,
            TitleJa = block.TitleJa,
            BodyJa = block.BodyJa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBlock(CmsBlockSaveRequest model, IFormFile? imageFile, CancellationToken ct)
    {
        if (await IsCareersPageAsync(model.PageId, ct))
            return RedirectToAction("Index", "Posts", new { kind = "Job", area = "Admin" });

        ViewBag.PageId = model.PageId;
        if (imageFile is { Length: > 0 })
        {
            await using var stream = imageFile.OpenReadStream();
            var uploaded = await media.SaveImageAsync(stream, imageFile.FileName, imageFile.ContentType, User.Identity?.Name, ct, "cms");
            model.MediaFileId = uploaded.Id;
        }

        var (ok, error, _) = await cms.SaveBlockAsync(model, ct);
        if (!ok)
        {
            TempData["Error"] = error;
            return View(model);
        }
        TempData["Success"] = "Đã lưu block.";
        return RedirectToAction(nameof(Blocks), new { id = model.PageId, area = "Admin" });
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
            var result = await media.SaveImageAsync(
                stream, file.FileName, file.ContentType, User.Identity?.Name, ct, "cms");
            return Json(new { ok = true, id = result.Id, url = result.Url, fileName = result.FileName });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlock(int pageId, int id, CancellationToken ct)
    {
        var (ok, error) = await cms.DeleteBlockAsync(id, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã xóa block." : error;
        return RedirectToAction(nameof(Blocks), new { id = pageId, area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveBlock(int pageId, int id, int direction, CancellationToken ct)
    {
        await cms.MoveBlockAsync(id, direction, ct);
        return RedirectToAction(nameof(Blocks), new { id = pageId, area = "Admin" });
    }

    private async Task<bool> PageExists(int pageId, CancellationToken ct)
        => await cms.GetPageAsync(pageId, ct) is not null;

    private async Task<bool> IsCareersPageAsync(int pageId, CancellationToken ct)
    {
        var page = await cms.GetPageAsync(pageId, ct);
        return page is not null &&
               string.Equals(page.ModuleCode, "careers", StringComparison.OrdinalIgnoreCase);
    }
}
