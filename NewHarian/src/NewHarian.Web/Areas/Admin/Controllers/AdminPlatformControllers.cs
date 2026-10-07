using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Application.Shipping;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[HasPermission(Permissions.Shipping.Manage)]
public class ShippingController(IAdminShippingService shipping) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var rows = await shipping.ListAsync(ct);
        var (items, pager) = AdminPaging.Apply(rows, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int provinceId, decimal fee, bool isActive, CancellationToken ct)
    {
        if (!await shipping.SaveAsync(provinceId, fee, isActive, ct))
        {
            this.FlashError("Không tìm thấy tỉnh / thành để lưu phí ship.");
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        this.FlashSuccess("Đã lưu phí ship.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}

[Area("Admin")]
[HasPermission(Permissions.Media.Manage)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class MediaController(AppDbContext db, IMediaStorage media) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var query = db.MediaFiles.AsNoTracking()
            .Where(m => !m.IsPrivate)
            .OrderByDescending(m => m.CreatedAt);
        var total = await query.CountAsync(ct);
        var pager = AdminPaging.Create(total, page);
        var items = await query.Skip(pager.Offset).Take(pager.PageSize).ToListAsync(ct);
        ViewBag.Pager = pager;
        return View(items);
    }

    // Start/Done logged in LocalMediaStorage — avoid duplicate controller logs
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            this.FlashError("Chọn file.");
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        try
        {
            await using var stream = file.OpenReadStream();
            await media.SaveImageAsync(stream, file.FileName, file.ContentType, User.Identity?.Name, ct, "media");
        }
        catch (InvalidOperationException ex)
        {
            this.FlashError(ex.Message);
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        this.FlashSuccess("Đã upload.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}

[Area("Admin")]
[HasPermission(Permissions.Menus.Manage)]
public class MenusController(IMenuAdminService menus) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await menus.GetHeaderMenuAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int itemId, bool isActive, CancellationToken ct)
    {
        var ok = await menus.SetActiveAsync(itemId, isActive, ct);
        this.FlashResult(ok, isActive ? "Đã hiện mục menu." : "Đã ẩn mục menu.", "Không tìm thấy mục menu.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveItem(int itemId, int direction, CancellationToken ct)
    {
        var ok = await menus.MoveItemAsync(itemId, direction, ct);
        this.FlashResult(ok, "Đã đổi thứ tự menu.", "Không đổi được thứ tự mục menu.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}

[Area("Admin")]
[HasPermission(Permissions.HomeSlides.Manage)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class HomeSlidesController(IHomeSlideAdminService slides) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await slides.ListAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? captionVi, string? linkUrl, bool isActive, IFormFile? image, CancellationToken ct)
    {
        if (image is { Length: > 0 })
        {
            await using var stream = image.OpenReadStream();
            await slides.CreateAsync(captionVi, linkUrl, isActive, new HomeSlideImageUpload(
                stream, image.FileName, image.ContentType, User.Identity?.Name), ct);
        }
        else
        {
            await slides.CreateAsync(captionVi, linkUrl, isActive, null, ct);
        }
        this.FlashSuccess("Đã thêm slide.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        await slides.MoveAsync(id, direction, ct);
        this.FlashSuccess("Đã đổi thứ tự slide.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await slides.DeleteAsync(id, ct);
        this.FlashSuccess("Đã xóa slide.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
