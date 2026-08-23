using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Application.Shipping;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
            return NotFound();
        TempData["Success"] = "Đã lưu phí ship.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
            TempData["Error"] = "Chọn file.";
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        await using var stream = file.OpenReadStream();
        await media.SaveImageAsync(stream, file.FileName, file.ContentType, User.Identity?.Name, ct, "media");
        TempData["Success"] = "Đã upload.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class UsersController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    ILogger<UsersController> logger) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var list = users.Users.OrderBy(u => u.Email).ToList();
        var vm = new List<UserRow>();
        foreach (var u in list)
        {
            var r = await users.GetRolesAsync(u);
            vm.Add(new UserRow(u.Id, u.Email ?? "", u.FullName, u.IsActive, string.Join(", ", r)));
        }
        var (items, pager) = AdminPaging.Apply(vm, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserVm model)
    {
        logger.LogInformation("CreateUser Start Email={Email}", model.Email);
        try
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                logger.LogWarning("CreateUser Done rejected Error={Error}", "Email và mật khẩu bắt buộc.");
                ModelState.AddModelError("", "Email và mật khẩu bắt buộc.");
                return View(model);
            }
            var role = model.Role is "Staff" or "Admin" ? model.Role : "Staff";
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = true,
                FullName = model.FullName?.Trim() ?? model.Email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await users.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                var errs = string.Join("; ", result.Errors.Select(e => e.Description));
                logger.LogWarning("CreateUser Done rejected Email={Email} Error={Error}", model.Email, errs);
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(model);
            }
            await users.AddToRoleAsync(user, role);
            logger.LogInformation("CreateUser Done Email={Email} UserId={UserId} Role={Role}", model.Email, user.Id, role);
            TempData["Success"] = "Đã tạo user.";
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateUser Error Email={Email}", model.Email);
            throw;
        }
    }

    public record UserRow(string Id, string Email, string? FullName, bool IsActive, string Roles);
    public class CreateUserVm
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FullName { get; set; }
        public string Role { get; set; } = "Staff";
    }
}

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class MenusController(IMenuAdminService menus) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await menus.GetHeaderMenuAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int itemId, bool isActive, CancellationToken ct)
    {
        if (!await menus.SetActiveAsync(itemId, isActive, ct))
            return NotFound();
        TempData["Success"] = isActive ? "Đã hiện mục menu." : "Đã ẩn mục menu.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveItem(int itemId, int direction, CancellationToken ct)
    {
        if (!await menus.MoveItemAsync(itemId, direction, ct))
            return NotFound();
        TempData["Success"] = "Đã đổi thứ tự menu.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
        TempData["Success"] = "Đã thêm slide.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        await slides.MoveAsync(id, direction, ct);
        return AdminListRedirect.ToRefererOrIndex(this);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await slides.DeleteAsync(id, ct);
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
