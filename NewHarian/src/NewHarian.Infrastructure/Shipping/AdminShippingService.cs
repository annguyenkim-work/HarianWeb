using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Shipping;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Shipping;

public sealed class AdminShippingService(
    AppDbContext db,
    ILogger<AdminShippingService> logger) : IAdminShippingService
{
    public async Task<IReadOnlyList<AdminShippingRow>> ListAsync(CancellationToken ct = default)
        => await db.ShippingProvinces.AsNoTracking()
            .Include(p => p.Rate)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.NameVi)
            .Select(p => new AdminShippingRow(
                p.Id, p.Code, p.NameVi, p.Rate != null ? p.Rate.Fee : 0m, p.IsActive))
            .ToListAsync(ct);

    public async Task<bool> SaveAsync(
        int provinceId,
        decimal fee,
        bool isActive,
        CancellationToken ct = default)
    {
        logger.LogInformation("SaveShipping Start ProvinceId={ProvinceId}", provinceId);
        try
        {
            var province = await db.ShippingProvinces
                .Include(p => p.Rate)
                .FirstOrDefaultAsync(p => p.Id == provinceId, ct);
            if (province is null)
            {
                logger.LogWarning("SaveShipping Done rejected ProvinceId={ProvinceId}", provinceId);
                return false;
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
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveShipping Error ProvinceId={ProvinceId}", provinceId);
            throw;
        }
    }
}
