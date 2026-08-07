using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Engagement;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class InquiriesController(IInquiryService inquiries) : Controller
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "email", "phone", "status"
    };

    public async Task<IActionResult> Index(
        InquiryStatus? status,
        string? q,
        string? sort,
        string? dir,
        int page = 1,
        CancellationToken ct = default)
    {
        sort = AdminListQuery.NormalizeSort(sort, SortKeys, "createdAt");
        dir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sort));

        ViewBag.Status = status;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        var (items, pager) = AdminPaging.Apply(
            await inquiries.ListAsync(status, q, sort, dir, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await inquiries.GetAsync(id, ct);
        if (item is null) return NotFound();
        return PartialView("_Detail", item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, InquiryStatus status, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await inquiries.UpdateStatusAsync(id, status, internalNotes, User.Identity?.Name, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật." : error;
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
