using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Enums;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class DealersController(
    IAdminDealerService dealers,
    IDealerDiscountOverrideService discountOverrides,
    IDealerAccessCodeService accessCodes,
    IMediaStorage media) : Controller
{
    private const string MediaFolder = "dealers";

    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "taxCode", "phone", "status"
    };

    [HasPermission(Permissions.Dealers.View)]
    public async Task<IActionResult> Index(
        DealerStatus? status,
        DealerBusinessType? type,
        string? q,
        string? sort,
        string? dir,
        int page = 1,
        CancellationToken ct = default)
    {
        sort = AdminListQuery.NormalizeSort(sort, SortKeys, "createdAt");
        dir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sort));
        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        var (items, pager) = AdminPaging.Apply(await dealers.ListAsync(status, type, q, sort, dir, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Dealers.View)]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await dealers.GetAsync(id, ct);
        if (item is null) return NotFound();
        ViewBag.Discounts = await discountOverrides.GetAsync(id, ct);
        return PartialView("_Detail", item);
    }

    [HttpGet]
    [HasPermission(Permissions.Dealers.View)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (!CanSave(id)) return Forbid();
        if (id is null) return PartialView("_Form", new DealerEditRequest());
        var model = await dealers.GetEditAsync(id.Value, ct);
        return model is null ? NotFound() : PartialView("_Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.View)]
    [RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
    public async Task<IActionResult> Save(DealerEditRequest model, IFormFile? businessLicenseFile, CancellationToken ct)
    {
        if (!CanSave(model.Id)) return Forbid();

        if (businessLicenseFile is { Length: > 0 })
        {
            try
            {
                await using var stream = businessLicenseFile.OpenReadStream();
                var up = await media.SaveDocumentAsync(
                    stream, businessLicenseFile.FileName, businessLicenseFile.ContentType, User.Identity?.Name, ct, MediaFolder);
                model.BusinessLicenseMediaFileId = up.Id;
                model.BusinessLicenseFileName = up.FileName;
                model.RemoveBusinessLicense = false;
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return PartialView("_Form", model);
            }
        }

        var (ok, error, _, warning) = await dealers.SaveAsync(model, User.Identity?.Name, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            return PartialView("_Form", model);
        }
        return Json(new
        {
            ok = true,
            message = model.Id is null ? "Đã thêm đại lý và gửi mã đại lý qua email." : "Đã cập nhật đại lý.",
            warning,
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }

    [HttpGet]
    [HasPermission(Permissions.Dealers.View)]
    public async Task<IActionResult> Document(int id, CancellationToken ct)
    {
        var mediaId = await dealers.GetBusinessLicenseMediaIdAsync(id, ct);
        if (mediaId is null) return NotFound();
        var opened = await media.OpenAsync(mediaId.Value, ct);
        if (opened is null) return NotFound();
        return File(opened.Content, opened.ContentType, opened.DownloadFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.Approve)]
    public async Task<IActionResult> Approve(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error, warning) = await dealers.ApproveAsync(id, internalNotes, User.Identity?.Name, ct);
        return ok
            ? Json(new { ok = true, message = "Đã duyệt đại lý và gửi mã đại lý qua email.", warning })
            : this.JsonFail(error);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.Approve)]
    public async Task<IActionResult> IssueCode(int id, CancellationToken ct)
    {
        var result = await accessCodes.IssueAsync(id, User.Identity?.Name, ct);
        if (!result.Ok) return this.JsonFail(result.Error);
        return Json(new
        {
            ok = true,
            message = "Đã cấp mã đại lý mới. Mã cũ không còn hiệu lực.",
            warning = result.EmailQueued ? null : DealerCodeMessages.EmailNotQueued
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.Approve)]
    public async Task<IActionResult> Reject(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await dealers.RejectAsync(id, internalNotes, User.Identity?.Name, ct);
        return ok ? Json(new { ok = true, message = "Đã từ chối đại lý." }) : this.JsonFail(error);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.Approve)]
    public async Task<IActionResult> SaveNotes(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await dealers.SaveNotesAsync(id, internalNotes, User.Identity?.Name, ct);
        return ok ? Json(new { ok = true, message = "Đã lưu ghi chú." }) : this.JsonFail(error);
    }

    [HttpGet]
    [HasPermission(Permissions.Dealers.ManageDiscounts)]
    public async Task<IActionResult> EditOverrides(int id, CancellationToken ct)
    {
        var model = await discountOverrides.GetAsync(id, ct);
        return model is null ? NotFound() : PartialView("_OverridesForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.ManageDiscounts)]
    public async Task<IActionResult> SaveOverrides(DealerDiscountOverridesInput model, CancellationToken ct)
    {
        var (ok, error) = await discountOverrides.SaveAsync(model, User.Identity?.Name, ct);
        return ok
            ? Json(new { ok = true, message = "Đã cập nhật % riêng.", redirect = Url.Action(nameof(Index), new { area = "Admin" }) })
            : this.JsonFail(error);
    }

    private bool CanSave(int? id)
        => User.HasPermission(id is null ? Permissions.Dealers.Create : Permissions.Dealers.Edit);
}
