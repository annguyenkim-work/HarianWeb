using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.DealerDiscounts;

public sealed class DealerDiscountOverrideService(
    AppDbContext db,
    IAuditService audit,
    ILogger<DealerDiscountOverrideService> logger) : IDealerDiscountOverrideService
{
    public async Task<DealerDiscountOverridesDto?> GetAsync(int dealerId, CancellationToken ct = default)
    {
        var dealer = await db.Dealers.AsNoTracking()
            .Where(d => d.Id == dealerId)
            .Select(d => new { d.Id, d.BusinessName })
            .FirstOrDefaultAsync(ct);
        if (dealer is null) return null;

        var groups = await DealerDiscountQueries.LoadGroupsAsync(db, ct);
        var overrides = await DealerDiscountQueries.LoadOverridesAsync(db, dealerId, ct);
        return new DealerDiscountOverridesDto(dealer.Id, dealer.BusinessName, groups, overrides);
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(DealerDiscountOverridesInput input, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveDealerDiscountOverrides Start DealerId={DealerId}", input.DealerId);
        try
        {
            var err = await ValidateAsync(input, ct);
            if (err is not null)
            {
                logger.LogWarning("SaveDealerDiscountOverrides Done rejected DealerId={DealerId} Error={Error}", input.DealerId, err);
                return (false, err);
            }

            var existing = await db.DealerDiscountOverrides.Where(o => o.DealerId == input.DealerId).ToListAsync(ct);
            var before = existing.Select(o => new { o.TierId, o.ProductId, o.Percent }).ToList();
            var now = DateTime.UtcNow;
            foreach (var cell in input.Cells)
            {
                var row = existing.FirstOrDefault(o => o.TierId == cell.TierId && o.ProductId == cell.ProductId);
                if (cell.Percent is null)
                {
                    if (row is not null)
                    {
                        db.DealerDiscountOverrides.Remove(row);
                        existing.Remove(row);
                    }
                    continue;
                }

                if (row is null)
                {
                    row = new DealerDiscountOverride
                    {
                        DealerId = input.DealerId,
                        TierId = cell.TierId,
                        ProductId = cell.ProductId
                    };
                    db.DealerDiscountOverrides.Add(row);
                    existing.Add(row);
                }
                if (row.Percent != cell.Percent.Value || row.Id == 0)
                {
                    row.Percent = cell.Percent.Value;
                    row.UpdatedAt = now;
                    row.UpdatedByUserId = userId;
                }
            }

            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.DiscountOverridesSaved", "Dealer", input.DealerId.ToString(),
                before, existing.Select(o => new { o.TierId, o.ProductId, o.Percent }).ToList(), ct);
            logger.LogInformation("SaveDealerDiscountOverrides Done DealerId={DealerId} Count={Count}", input.DealerId, existing.Count);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveDealerDiscountOverrides Error DealerId={DealerId}", input.DealerId);
            throw;
        }
    }

    private async Task<string?> ValidateAsync(DealerDiscountOverridesInput input, CancellationToken ct)
    {
        if (!await db.Dealers.AnyAsync(d => d.Id == input.DealerId, ct))
            return "Không tìm thấy đại lý.";
        if (input.Cells.Any(c => c.Percent is decimal p && !DealerDiscountGroupValidator.IsPercent(p)))
            return "% phải từ 0 đến 100, tối đa 2 chữ số thập phân.";
        if (input.Cells.GroupBy(c => (c.TierId, c.ProductId)).Any(g => g.Count() > 1))
            return "Ô chiết khấu bị trùng.";

        var tierIds = input.Cells.Select(c => c.TierId).Distinct().ToList();
        var validCells = await db.DealerDiscountTiers.AsNoTracking()
            .Where(t => tierIds.Contains(t.Id))
            .SelectMany(t => t.Group.Products.Select(p => new { TierId = t.Id, p.ProductId }))
            .ToListAsync(ct);
        var valid = validCells.Select(x => (x.TierId, x.ProductId)).ToHashSet();
        return input.Cells.All(c => valid.Contains((c.TierId, c.ProductId)))
            ? null
            : "Bảng chiết khấu đã thay đổi. Vui lòng mở lại.";
    }
}
