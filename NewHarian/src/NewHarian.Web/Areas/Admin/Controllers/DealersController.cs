using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class DealersController(IDealerService dealers) : Controller
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "email", "phone", "status"
    };

    public async Task<IActionResult> Index(
        DealerStatus? status,
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
        var (items, pager) = AdminPaging.Apply(await dealers.ListAsync(status, q, sort, dir, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await dealers.GetAsync(id, ct);
        if (item is null) return NotFound();
        return PartialView("_Detail", item);
    }

    [HttpGet]
    public IActionResult Create() => PartialView("_CreateForm", new DealerCreateRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DealerCreateRequest model, CancellationToken ct)
    {
        var (ok, error, _) = await dealers.CreateApprovedAsync(model, User.Identity?.Name, ct);
        if (Request.Headers.Accept.ToString().Contains("application/json")
            || Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { ok, error });
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không tạo được.");
            return PartialView("_CreateForm", model);
        }
        TempData["Success"] = "Đã thêm đại lý.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, decimal discountPercent, string? internalNotes, string? citizenId, CancellationToken ct)
    {
        var (ok, error) = await dealers.ApproveAsync(id, discountPercent, internalNotes, User.Identity?.Name, citizenId, ct);
        return Json(new { ok, error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await dealers.RejectAsync(id, internalNotes, User.Identity?.Name, ct);
        return Json(new { ok, error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, decimal discountPercent, string? internalNotes, string? citizenId, CancellationToken ct)
    {
        var (ok, error) = await dealers.SaveApprovedAsync(id, discountPercent, internalNotes, User.Identity?.Name, citizenId, ct);
        return Json(new { ok, error });
    }
}
