using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Cms;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Cms;

public sealed class MenuAdminService(
    AppDbContext db,
    ISiteChromeCache chrome,
    ILogger<MenuAdminService> logger) : IMenuAdminService
{
    private const string HeaderMenuCode = "header-main";

    public Task<Menu?> GetHeaderMenuAsync(CancellationToken ct = default)
        => db.Menus.AsNoTracking()
            .Include(m => m.Items).ThenInclude(i => i.Translations)
            .Include(m => m.Items).ThenInclude(i => i.Children).ThenInclude(c => c.Translations)
            .FirstOrDefaultAsync(m => m.Code == HeaderMenuCode, ct);

    public async Task<bool> SetActiveAsync(int itemId, bool isActive, CancellationToken ct = default)
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
                return false;
            }

            item.IsActive = isActive;
            await db.SaveChangesAsync(ct);
            chrome.InvalidateMenus();
            logger.LogInformation("SetMenuItemActive Done Id={Id} Active={Active}", itemId, isActive);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SetMenuItemActive Error Id={Id}", itemId);
            throw;
        }
    }

    public async Task<bool> MoveItemAsync(int itemId, int direction, CancellationToken ct = default)
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
                return false;
            }

            var siblings = await db.MenuItems
                .Where(i => i.MenuId == item.MenuId && i.ParentId == item.ParentId)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .ToListAsync(ct);
            var index = siblings.FindIndex(i => i.Id == itemId);
            var swapIndex = index + direction;
            if (index >= 0 && swapIndex >= 0 && swapIndex < siblings.Count)
            {
                (siblings[index].SortOrder, siblings[swapIndex].SortOrder) =
                    (siblings[swapIndex].SortOrder, siblings[index].SortOrder);
                await db.SaveChangesAsync(ct);
                chrome.InvalidateMenus();
            }
            logger.LogInformation("MoveMenuItem Done Id={Id}", itemId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveMenuItem Error Id={Id}", itemId);
            throw;
        }
    }
}
