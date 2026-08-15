using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ShippingController(AppDbContext db, ILogger<ShippingController> logger) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var rows = await db.ShippingProvinces.AsNoTracking()
            .Include(p => p.Rate)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.NameVi)
            .Select(p => new ShippingRow(p.Id, p.NameVi, p.Rate != null ? p.Rate.Fee : 0m, p.IsActive))
            .ToListAsync(ct);
        var (items, pager) = AdminPaging.Apply(rows, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int provinceId, decimal fee, bool isActive, CancellationToken ct)
    {
        logger.LogInformation("SaveShipping Start ProvinceId={ProvinceId}", provinceId);
        try
        {
            var province = await db.ShippingProvinces.Include(p => p.Rate).FirstOrDefaultAsync(p => p.Id == provinceId, ct);
            if (province is null)
            {
                logger.LogWarning("SaveShipping Done rejected ProvinceId={ProvinceId}", provinceId);
                return NotFound();
            }
            province.IsActive = isActive;
            if (province.Rate is null)
            {
                province.Rate = new ShippingRate { ProvinceId = provinceId, Fee = fee };
                db.ShippingRates.Add(province.Rate);
            }
            else
            {
                province.Rate.Fee = fee;
                province.Rate.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveShipping Done ProvinceId={ProvinceId} Fee={Fee}", provinceId, fee);
            TempData["Success"] = "Đã lưu phí ship.";
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveShipping Error ProvinceId={ProvinceId}", provinceId);
            throw;
        }
    }

    public record ShippingRow(int ProvinceId, string Name, decimal Fee, bool IsActive);
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
public class MenusController(AppDbContext db, ISiteChromeCache chrome, ILogger<MenusController> logger) : Controller
{
    public const string HeaderMenuCode = "header-main";

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var menu = await db.Menus.AsNoTracking()
            .Include(m => m.Items).ThenInclude(i => i.Translations)
            .Include(m => m.Items).ThenInclude(i => i.Children).ThenInclude(c => c.Translations)
            .FirstOrDefaultAsync(m => m.Code == HeaderMenuCode, ct);

        return View(menu);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int itemId, bool isActive, CancellationToken ct)
    {
        logger.LogInformation("SetMenuItemActive Start Id={Id} Active={Active}", itemId, isActive);
        try
        {
            var item = await db.MenuItems
                .Include(i => i.Menu)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Menu!.Code == HeaderMenuCode, ct);
            if (item is null)
            {
                logger.LogWarning("SetMenuItemActive Done rejected Id={Id}", itemId);
                return NotFound();
            }

            item.IsActive = isActive;
            await db.SaveChangesAsync(ct);
            chrome.InvalidateMenus();
            logger.LogInformation("SetMenuItemActive Done Id={Id} Active={Active}", itemId, isActive);
            TempData["Success"] = isActive ? "Đã hiện mục menu." : "Đã ẩn mục menu.";
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SetMenuItemActive Error Id={Id}", itemId);
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveItem(int itemId, int direction, CancellationToken ct)
    {
        logger.LogInformation("MoveMenuItem Start Id={Id} Direction={Direction}", itemId, direction);
        try
        {
            var item = await db.MenuItems.AsNoTracking()
                .Include(i => i.Menu)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Menu!.Code == HeaderMenuCode, ct);
            if (item is null)
            {
                logger.LogWarning("MoveMenuItem Done rejected Id={Id}", itemId);
                return NotFound();
            }

            var siblings = await db.MenuItems
                .Where(i => i.MenuId == item.MenuId && i.ParentId == item.ParentId)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .ToListAsync(ct);
            var idx = siblings.FindIndex(i => i.Id == itemId);
            var swapIdx = idx + direction;
            if (idx >= 0 && swapIdx >= 0 && swapIdx < siblings.Count)
            {
                (siblings[idx].SortOrder, siblings[swapIdx].SortOrder) = (siblings[swapIdx].SortOrder, siblings[idx].SortOrder);
                await db.SaveChangesAsync(ct);
                chrome.InvalidateMenus();
            }
            logger.LogInformation("MoveMenuItem Done Id={Id}", itemId);
            TempData["Success"] = "Đã đổi thứ tự menu.";
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveMenuItem Error Id={Id}", itemId);
            throw;
        }
    }
}

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class HomeSlidesController(AppDbContext db, IMediaStorage media, ILogger<HomeSlidesController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await db.HomeSlides.AsNoTracking().Include(s => s.Translations).OrderBy(s => s.SortOrder).ToListAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? captionVi, string? linkUrl, bool isActive, IFormFile? image, CancellationToken ct)
    {
        logger.LogInformation("CreateHomeSlide Start");
        try
        {
            string? imageUrl = null;
            int? mediaId = null;
            if (image is { Length: > 0 })
            {
                await using var stream = image.OpenReadStream();
                var up = await media.SaveImageAsync(stream, image.FileName, image.ContentType, User.Identity?.Name, ct, "slides");
                imageUrl = up.Url;
                mediaId = up.Id;
            }

            var maxOrder = await db.HomeSlides.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0;
            db.HomeSlides.Add(new HomeSlide
            {
                ImageUrl = imageUrl,
                MediaFileId = mediaId,
                LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
                SortOrder = maxOrder + 1,
                IsActive = isActive,
                Translations = { new HomeSlideTranslation { LanguageCode = "vi", Caption = captionVi } }
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("CreateHomeSlide Done MediaId={MediaId}", mediaId);
            TempData["Success"] = "Đã thêm slide.";
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateHomeSlide Error");
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        logger.LogInformation("MoveHomeSlide Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var slides = await db.HomeSlides.OrderBy(s => s.SortOrder).ToListAsync(ct);
            var idx = slides.FindIndex(s => s.Id == id);
            var swapIdx = idx + direction;
            if (idx >= 0 && swapIdx >= 0 && swapIdx < slides.Count)
            {
                (slides[idx].SortOrder, slides[swapIdx].SortOrder) = (slides[swapIdx].SortOrder, slides[idx].SortOrder);
                await db.SaveChangesAsync(ct);
            }
            logger.LogInformation("MoveHomeSlide Done Id={Id}", id);
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveHomeSlide Error Id={Id}", id);
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        logger.LogInformation("DeleteHomeSlide Start Id={Id}", id);
        try
        {
            var s = await db.HomeSlides.FindAsync([id], ct);
            if (s is not null)
            {
                db.HomeSlides.Remove(s);
                await db.SaveChangesAsync(ct);
            }
            logger.LogInformation("DeleteHomeSlide Done Id={Id}", id);
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteHomeSlide Error Id={Id}", id);
            throw;
        }
    }
}
