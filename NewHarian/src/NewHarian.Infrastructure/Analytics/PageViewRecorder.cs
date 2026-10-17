using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Analytics;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Analytics;

public sealed class PageViewRecorder(AppDbContext db, ILogger<PageViewRecorder> logger) : IPageViewRecorder
{
    public async Task RecordAsync(PageKind pageKind, int? productId = null, CancellationToken ct = default)
    {
        var date = AnalyticsTime.TodayLocal();
        try
        {
            await IncrementPageAsync(date, pageKind, ct);
            if (productId is int pid && pageKind == PageKind.ProductDetail)
                await IncrementProductAsync(date, pid, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PageViewRecorder Error PageKind={PageKind} ProductId={ProductId}", pageKind, productId);
        }
    }

    private async Task IncrementPageAsync(DateOnly date, PageKind pageKind, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var row = await db.PageViewDaily.FirstOrDefaultAsync(x => x.Date == date && x.PageKind == pageKind, ct);
            if (row is null)
            {
                db.PageViewDaily.Add(new PageViewDaily { Date = date, PageKind = pageKind, ViewCount = 1 });
            }
            else
            {
                row.ViewCount++;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }

    private async Task IncrementProductAsync(DateOnly date, int productId, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct)) return;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var row = await db.ProductViewDaily.FirstOrDefaultAsync(x => x.Date == date && x.ProductId == productId, ct);
            if (row is null)
            {
                db.ProductViewDaily.Add(new ProductViewDaily { Date = date, ProductId = productId, ViewCount = 1 });
            }
            else
            {
                row.ViewCount++;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }
}
