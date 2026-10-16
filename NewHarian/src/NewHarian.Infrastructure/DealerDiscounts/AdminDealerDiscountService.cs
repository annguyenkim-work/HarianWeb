using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.DealerDiscounts;

public sealed class AdminDealerDiscountService(
    AppDbContext db,
    IAuditService audit,
    ILogger<AdminDealerDiscountService> logger) : IAdminDealerDiscountService
{
    public async Task<DealerDiscountOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var groups = await DealerDiscountQueries.LoadGroupsAsync(db, ct);
        var grouped = groups.SelectMany(g => g.Products.Select(p => p.ProductId)).ToHashSet();
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Translations)
            .Where(p => p.Status == ProductStatus.Published)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);
        var unconfigured = products
            .Where(p => !grouped.Contains(p.Id))
            .Select(p => new DealerDiscountProductDto(p.Id, DealerDiscountQueries.ProductName(p)))
            .ToList();
        return new DealerDiscountOverviewDto(groups, unconfigured);
    }

    public async Task<IReadOnlyList<DealerDiscountProductOptionDto>> ListProductOptionsAsync(int? groupId, CancellationToken ct = default)
    {
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Translations)
            .Where(p => p.Status != ProductStatus.Archived)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);
        var membership = await db.DealerDiscountGroupProducts.AsNoTracking()
            .Where(x => groupId == null || x.GroupId != groupId)
            .Select(x => new { x.ProductId, x.Group.Name })
            .ToDictionaryAsync(x => x.ProductId, x => x.Name, ct);
        return products
            .Select(p => new DealerDiscountProductOptionDto(
                p.Id, DealerDiscountQueries.ProductName(p), membership.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<DealerDiscountGroupEditModel> GetGroupEditAsync(int? id, int? preselectProductId = null, CancellationToken ct = default)
    {
        var model = new DealerDiscountGroupEditModel
        {
            ProductOptions = await ListProductOptionsAsync(id, ct)
        };

        if (id is int gid)
        {
            var g = (await DealerDiscountQueries.LoadGroupsAsync(db, ct)).FirstOrDefault(x => x.Id == gid);
            if (g is not null)
            {
                model.Input = new DealerDiscountGroupInput
                {
                    Id = g.Id,
                    Name = g.Name,
                    ProductIds = g.Products.Select(p => p.ProductId).ToList(),
                    Tiers = g.Tiers.Select(t => new DealerDiscountTierInput
                    {
                        Id = t.Id,
                        MinQuantity = t.MinQuantity,
                        MaxQuantity = t.MaxQuantity,
                        Percents = g.Products.ToDictionary(
                            p => p.ProductId,
                            p => g.Rates.TryGetValue((t.Id, p.ProductId), out var v) ? (decimal?)v : null)
                    }).ToList()
                };
                model.DealersWithOverrides = await db.DealerDiscountOverrides.AsNoTracking()
                    .Where(o => o.Tier.GroupId == gid)
                    .Select(o => o.DealerId).Distinct().CountAsync(ct);
            }
            return model;
        }

        model.Input = new DealerDiscountGroupInput
        {
            Tiers = [new DealerDiscountTierInput { MinQuantity = 1 }]
        };
        if (preselectProductId is int pid && model.ProductOptions.Any(p => p.ProductId == pid && p.GroupName is null))
        {
            model.Input.ProductIds.Add(pid);
            model.Input.Name = model.ProductOptions.First(p => p.ProductId == pid).Name;
        }
        return model;
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveGroupAsync(DealerDiscountGroupInput input, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveDealerDiscountGroup Start Id={Id}", input.Id);
        try
        {
            input.Name = input.Name?.Trim() ?? "";
            var err = DealerDiscountGroupValidator.Validate(input) ?? await ValidateAgainstDbAsync(input, ct);
            DealerDiscountGroup? group = null;
            if (err is null && input.Id is int id)
            {
                group = await db.DealerDiscountGroups
                    .Include(g => g.Products)
                    .Include(g => g.Tiers).ThenInclude(t => t.Rates)
                    .FirstOrDefaultAsync(g => g.Id == id, ct);
                if (group is null) err = "Không tìm thấy nhóm.";
            }
            if (err is null && input.Tiers.Any(t => t.Id is int tid && group?.Tiers.All(x => x.Id != tid) != false))
                err = "Mức không thuộc nhóm này.";
            if (err is not null)
            {
                logger.LogWarning("SaveDealerDiscountGroup Done rejected Id={Id} Error={Error}", input.Id, err);
                return (false, err, null);
            }

            var before = group is null ? null : Snapshot(group);
            var now = DateTime.UtcNow;
            if (group is null)
            {
                var maxSort = await db.DealerDiscountGroups.MaxAsync(g => (int?)g.SortOrder, ct) ?? 0;
                group = new DealerDiscountGroup { SortOrder = maxSort + 1, CreatedAt = now };
                db.DealerDiscountGroups.Add(group);
            }
            else
            {
                group.UpdatedAt = now;
            }
            group.Name = input.Name;

            await ApplyProductsAsync(group, input.ProductIds, ct);
            await ApplyTiersAsync(group, input, ct);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync("DealerDiscountGroup.Saved", "DealerDiscountGroup", group.Id.ToString(),
                before, Snapshot(group), ct);
            logger.LogInformation("SaveDealerDiscountGroup Done Id={Id}", group.Id);
            return (true, null, group.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveDealerDiscountGroup Error Id={Id}", input.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteGroupAsync(int id, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteDealerDiscountGroup Start Id={Id}", id);
        try
        {
            var group = await db.DealerDiscountGroups
                .Include(g => g.Products)
                .Include(g => g.Tiers).ThenInclude(t => t.Rates)
                .FirstOrDefaultAsync(g => g.Id == id, ct);
            if (group is null)
            {
                logger.LogWarning("DeleteDealerDiscountGroup Done rejected Id={Id}", id);
                return (false, "Không tìm thấy nhóm.");
            }

            var before = Snapshot(group);
            var tierIds = group.Tiers.Select(t => t.Id).ToList();
            db.DealerDiscountOverrides.RemoveRange(
                await db.DealerDiscountOverrides.Where(o => tierIds.Contains(o.TierId)).ToListAsync(ct));
            db.DealerDiscountGroups.Remove(group);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("DealerDiscountGroup.Deleted", "DealerDiscountGroup", id.ToString(), before, null, ct);
            logger.LogInformation("DeleteDealerDiscountGroup Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteDealerDiscountGroup Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> MoveGroupAsync(int id, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("ReorderDealerDiscountGroup Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var items = await db.DealerDiscountGroups.OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync(ct);
            var idx = items.FindIndex(g => g.Id == id);
            var swapIdx = idx + Math.Sign(direction);
            if (idx < 0 || swapIdx < 0 || swapIdx >= items.Count)
            {
                logger.LogWarning("ReorderDealerDiscountGroup Done rejected Id={Id}", id);
                return (false, idx < 0 ? "Không tìm thấy nhóm." : "Không thể di chuyển thêm.");
            }

            for (var i = 0; i < items.Count; i++)
                items[i].SortOrder = i + 1;
            (items[idx].SortOrder, items[swapIdx].SortOrder) = (items[swapIdx].SortOrder, items[idx].SortOrder);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("ReorderDealerDiscountGroup Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ReorderDealerDiscountGroup Error Id={Id}", id);
            throw;
        }
    }

    private async Task<string?> ValidateAgainstDbAsync(DealerDiscountGroupInput input, CancellationToken ct)
    {
        var nameLower = input.Name.ToLower();
        if (await db.DealerDiscountGroups.AnyAsync(g => g.Name.ToLower() == nameLower && g.Id != input.Id, ct))
            return "Tên nhóm đã tồn tại.";

        var existing = await db.Products.AsNoTracking()
            .Where(p => input.ProductIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (existing.Count != input.ProductIds.Distinct().Count())
            return "Sản phẩm không tồn tại.";

        var taken = await db.DealerDiscountGroupProducts.AsNoTracking()
            .Include(x => x.Group)
            .Include(x => x.Product).ThenInclude(p => p.Translations)
            .Where(x => input.ProductIds.Contains(x.ProductId) && x.GroupId != input.Id)
            .FirstOrDefaultAsync(ct);
        return taken is null
            ? null
            : $"Sản phẩm '{DealerDiscountQueries.ProductName(taken.Product)}' đã thuộc nhóm '{taken.Group.Name}'.";
    }

    private async Task ApplyProductsAsync(DealerDiscountGroup group, List<int> productIds, CancellationToken ct)
    {
        var removed = group.Products.Where(p => !productIds.Contains(p.ProductId)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(p => p.ProductId).ToList();
            var tierIds = group.Tiers.Select(t => t.Id).ToList();
            db.DealerDiscountOverrides.RemoveRange(await db.DealerDiscountOverrides
                .Where(o => tierIds.Contains(o.TierId) && removedIds.Contains(o.ProductId))
                .ToListAsync(ct));
            foreach (var p in removed)
                group.Products.Remove(p);
        }
        foreach (var pid in productIds.Where(pid => group.Products.All(p => p.ProductId != pid)))
            group.Products.Add(new DealerDiscountGroupProduct { ProductId = pid });
    }

    private async Task ApplyTiersAsync(DealerDiscountGroup group, DealerDiscountGroupInput input, CancellationToken ct)
    {
        var keptIds = input.Tiers.Where(t => t.Id is not null).Select(t => t.Id!.Value).ToHashSet();
        var removedTiers = group.Tiers.Where(t => !keptIds.Contains(t.Id)).ToList();
        if (removedTiers.Count > 0)
        {
            var removedIds = removedTiers.Select(t => t.Id).ToList();
            db.DealerDiscountOverrides.RemoveRange(await db.DealerDiscountOverrides
                .Where(o => removedIds.Contains(o.TierId))
                .ToListAsync(ct));
            foreach (var t in removedTiers)
            {
                db.DealerDiscountRates.RemoveRange(t.Rates);
                group.Tiers.Remove(t);
                db.DealerDiscountTiers.Remove(t);
            }
        }

        for (var i = 0; i < input.Tiers.Count; i++)
        {
            var src = input.Tiers[i];
            var tier = src.Id is int tid ? group.Tiers.First(t => t.Id == tid) : null;
            if (tier is null)
            {
                tier = new DealerDiscountTier();
                group.Tiers.Add(tier);
            }
            tier.MinQuantity = src.MinQuantity;
            tier.MaxQuantity = src.MaxQuantity;
            tier.SortOrder = i + 1;

            foreach (var stale in tier.Rates.Where(r => !input.ProductIds.Contains(r.ProductId)).ToList())
            {
                tier.Rates.Remove(stale);
                db.DealerDiscountRates.Remove(stale);
            }
            foreach (var pid in input.ProductIds)
            {
                var percent = src.Percents[pid]!.Value;
                var rate = tier.Rates.FirstOrDefault(r => r.ProductId == pid);
                if (rate is null)
                    tier.Rates.Add(new DealerDiscountRate { ProductId = pid, Percent = percent });
                else
                    rate.Percent = percent;
            }
        }
    }

    private static object Snapshot(DealerDiscountGroup g) => new
    {
        g.Name,
        Products = g.Products.Select(p => p.ProductId).OrderBy(x => x).ToList(),
        Tiers = g.Tiers.OrderBy(t => t.SortOrder).Select(t => new
        {
            t.MinQuantity,
            t.MaxQuantity,
            Rates = t.Rates.OrderBy(r => r.ProductId).Select(r => new { r.ProductId, r.Percent }).ToList()
        }).ToList()
    };
}
