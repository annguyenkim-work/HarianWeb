using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Engagement;
using NewHarian.Application.Posts;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class ApplicationsController(
    IJobApplicationService apps,
    IAdminSitePostService posts,
    IMediaStorage media,
    AppDbContext db,
    ILogger<ApplicationsController> logger) : Controller
{
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
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await apps.GetAsync(id, ct);
        if (item is null) return NotFound();
        return PartialView("_Detail", item);
    }

    /// <summary>Auth-gated CV download — files live outside wwwroot.</summary>
    [HttpGet]
    public async Task<IActionResult> Cv(int id, CancellationToken ct)
    {
        logger.LogInformation("DownloadApplicationCv Start ApplicationId={Id}", id);
        try
        {
            var application = await db.JobApplications.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, ct);
            if (application?.AttachmentMediaFileId is not int mediaId)
            {
                logger.LogWarning("DownloadApplicationCv Done rejected ApplicationId={Id} Error={Error}", id, "No attachment");
                return NotFound();
            }

            var opened = await media.OpenAsync(mediaId, ct);
            if (opened is null)
            {
                logger.LogWarning("DownloadApplicationCv Done rejected ApplicationId={Id} MediaId={MediaId} Error={Error}",
                    id, mediaId, "File missing");
                return NotFound();
            }

            logger.LogInformation("DownloadApplicationCv Done ApplicationId={Id} MediaId={MediaId}", id, mediaId);
            return File(opened.Content, opened.ContentType, opened.DownloadFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DownloadApplicationCv Error ApplicationId={Id}", id);
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ApplicationStatus status, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await apps.UpdateStatusAsync(id, status, internalNotes, User.Identity?.Name, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật." : error;
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
