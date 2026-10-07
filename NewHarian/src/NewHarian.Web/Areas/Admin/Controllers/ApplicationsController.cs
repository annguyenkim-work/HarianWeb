using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Engagement;
using NewHarian.Application.Posts;
using NewHarian.Domain.Enums;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class ApplicationsController(
    IJobApplicationService apps,
    IAdminSitePostService posts) : Controller
{
    [HasPermission(Permissions.Applications.View)]
    public async Task<IActionResult> Index(
        ApplicationStatus? status,
        int? sitePostId,
        string? q,
        string? sort,
        string? dir,
        int page = 1,
        CancellationToken ct = default)
    {
        sort = AdminListQuery.NormalizeSort(sort, ApplicationSortKeys, "createdAt");
        dir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sort));

        ViewBag.Status = status;
        ViewBag.SitePostId = sitePostId;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Jobs = await posts.ListJobOptionsAsync(ct);
        var (items, pager) = AdminPaging.Apply(
            await apps.ListAsync(status, sitePostId, q, sort, dir, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    private static readonly HashSet<string> ApplicationSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "email", "job", "type", "status", "hasCv"
    };

    [HttpGet]
    [HasPermission(Permissions.Applications.View)]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await apps.GetAsync(id, ct);
        if (item is null) return NotFound();
        return PartialView("_Detail", item);
    }

    /// <summary>Auth-gated CV download — files live outside wwwroot.</summary>
    [HttpGet]
    [HasPermission(Permissions.Applications.DownloadCv)]
    public async Task<IActionResult> Cv(int id, CancellationToken ct)
    {
        var opened = await apps.OpenCvAsync(id, ct);
        if (opened is null) return NotFound();
        return File(opened.Content, opened.ContentType, opened.DownloadFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Applications.Update)]
    public async Task<IActionResult> UpdateStatus(int id, ApplicationStatus status, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await apps.UpdateStatusAsync(id, status, internalNotes, User.Identity?.Name, ct);
        this.FlashResult(ok, "Đã cập nhật hồ sơ ứng tuyển.", error);
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
