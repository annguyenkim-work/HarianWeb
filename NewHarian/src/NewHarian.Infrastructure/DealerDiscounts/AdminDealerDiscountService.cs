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
        var grouped = groups.SelectMany(g => g.Skus.Select(s => s.ProductVariantId)).ToHashSet();
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p.Translations)
            .Where(v => v.IsActive && v.Product.Status == ProductStatus.Published)
            .OrderBy(v => v.Product.SortOrder).ThenBy(v => v.ProductId).ThenBy(v => v.Id)
            .ToListAsync(ct);
        var unconfigured = variants
            .Where(v => !grouped.Contains(v.Id))
            .Select(DealerDiscountQueries.ToSkuDto)
            .ToList();
        return new DealerDiscountOverviewDto(groups, unconfigured);
    }

    public async Task<IReadOnlyList<DealerDiscountSkuOptionDto>> ListSkuOptionsAsync(int? groupId, CancellationToken ct = default)
    {
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p.Translations)
            .Where(v => v.IsActive && v.Product.Status != ProductStatus.Archived)
            .OrderBy(v => v.Product.SortOrder).ThenBy(v => v.ProductId).ThenBy(v => v.Id)
            .ToListAsync(ct);
        var membership = await db.DealerDiscountGroupVariants.AsNoTracking()
            .Where(x => groupId == null || x.GroupId != groupId)
            .Select(x => new { x.ProductVariantId, x.Group.Name })
            .ToDictionaryAsync(x => x.ProductVariantId, x => x.Name, ct);
        return variants
            .Select(v => new DealerDiscountSkuOptionDto(
                v.Id, DealerDiscountQueries.SkuLabel(v), v.Sku, membership.GetValueOrDefault(v.Id)))
            .ToList();
    }

    public async Task<DealerDiscountGroupEditModel> GetGroupEditAsync(int? id, int? preselectVariantId = null, CancellationToken ct = default)
    {
        var model = new DealerDiscountGroupEditModel
        {
            SkuOptions = await ListSkuOptionsAsync(id, ct)
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
                    ProductVariantIds = g.Skus.Select(s => s.ProductVariantId).ToList(),
                    Tiers = g.Tiers.Select(t => new DealerDiscountTierInput
                    {
                        Id = t.Id,
                        MinQuantity = t.MinQuantity,
                        MaxQuantity = t.MaxQuantity,
                        Percents = g.Skus.ToDictionary(
                            s => s.ProductVariantId,
                            s => g.Rates.TryGetValue((t.Id, s.ProductVariantId), out var v) ? (decimal?)v : null)
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
        if (preselectVariantId is int vid
            && model.SkuOptions.FirstOrDefault(s => s.ProductVariantId == vid && s.GroupName is null) is { } opt)
        {
            model.Input.ProductVariantIds.Add(vid);
            model.Input.Name = opt.Sku;
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
                    .Include(g => g.Variants)
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

            await ApplyVariantsAsync(group, input.ProductVariantIds, ct);
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
                .Include(g => g.Variants)
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

        var existing = await db.ProductVariants.AsNoTracking()
            .Where(v => input.ProductVariantIds.Contains(v.Id) && v.IsActive)
            .Select(v => v.Id)
            .ToListAsync(ct);
        if (existing.Count != input.ProductVariantIds.Distinct().Count())
            return "SKU không tồn tại hoặc đã tắt.";

        var taken = await db.DealerDiscountGroupVariants.AsNoTracking()
            .Include(x => x.Group)
            .Include(x => x.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Translations)
            .Where(x => input.ProductVariantIds.Contains(x.ProductVariantId) && x.GroupId != input.Id)
            .FirstOrDefaultAsync(ct);
        return taken is null
            ? null
            : $"SKU '{DealerDiscountQueries.SkuLabel(taken.ProductVariant)}' đã thuộc nhóm '{taken.Group.Name}'.";
    }

    private async Task ApplyVariantsAsync(DealerDiscountGroup group, List<int> variantIds, CancellationToken ct)
    {
        var removed = group.Variants.Where(v => !variantIds.Contains(v.ProductVariantId)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(v => v.ProductVariantId).ToList();
            var tierIds = group.Tiers.Select(t => t.Id).ToList();
            db.DealerDiscountOverrides.RemoveRange(await db.DealerDiscountOverrides
                .Where(o => tierIds.Contains(o.TierId) && removedIds.Contains(o.ProductVariantId))
                .ToListAsync(ct));
            foreach (var v in removed)
                group.Variants.Remove(v);
        }
        foreach (var vid in variantIds.Where(vid => group.Variants.All(v => v.ProductVariantId != vid)))
            group.Variants.Add(new DealerDiscountGroupVariant { ProductVariantId = vid });
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

            foreach (var stale in tier.Rates.Where(r => !input.ProductVariantIds.Contains(r.ProductVariantId)).ToList())
            {
                tier.Rates.Remove(stale);
                db.DealerDiscountRates.Remove(stale);
            }
            foreach (var vid in input.ProductVariantIds)
            {
                var percent = src.Percents[vid]!.Value;
                var rate = tier.Rates.FirstOrDefault(r => r.ProductVariantId == vid);
                if (rate is null)
                    tier.Rates.Add(new DealerDiscountRate { ProductVariantId = vid, Percent = percent });
                else
                    rate.Percent = percent;
            }
        }
    }

    private static object Snapshot(DealerDiscountGroup g) => new
    {
        g.Name,
        Variants = g.Variants.Select(v => v.ProductVariantId).OrderBy(x => x).ToList(),
        Tiers = g.Tiers.OrderBy(t => t.SortOrder).Select(t => new
        {
            t.MinQuantity,
            t.MaxQuantity,
            Rates = t.Rates.OrderBy(r => r.ProductVariantId).Select(r => new { r.ProductVariantId, r.Percent }).ToList()
        }).ToList()
    };
}
