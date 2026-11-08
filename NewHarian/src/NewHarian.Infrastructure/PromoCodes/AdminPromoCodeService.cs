using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Formatting;
using NewHarian.Application.PromoCodes;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.PromoCodes;

public sealed class AdminPromoCodeService(AppDbContext db, ILogger<AdminPromoCodeService> logger) : IAdminPromoCodeService
{
    private static readonly string[] LocalFormats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"];

    public async Task<IReadOnlyList<PromoCodeListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await db.PromoCodes.AsNoTracking()
            .OrderBy(p => p.Code)
            .ToListAsync(ct);
        return rows.Select(p =>
        {
            var state = ToState(p);
            return new PromoCodeListItemDto(
                p.Id,
                p.Code,
                p.DiscountPercent,
                VietnamTime.Format(p.ValidFrom),
                VietnamTime.Format(p.ValidTo),
                p.MaxUses,
                p.UsedCount,
                p.IsActive,
                PromoCodeRules.StatusLabel(state, now));
        }).ToList();
    }

    public Task<PromoCodeSaveRequest> NewAsync()
    {
        var start = VietnamTime.ToLocal(DateTime.UtcNow);
        return Task.FromResult(new PromoCodeSaveRequest
        {
            ValidFromLocal = start.ToString("yyyy-MM-ddTHH:mm"),
            ValidToLocal = start.AddDays(30).ToString("yyyy-MM-ddTHH:mm"),
            MaxUses = 1,
            IsActive = true
        });
    }

    public async Task<PromoCodeSaveRequest?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.PromoCodes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null) return null;
        return new PromoCodeSaveRequest
        {
            Id = entity.Id,
            Code = entity.Code,
            DiscountPercent = entity.DiscountPercent,
            ValidFromLocal = VietnamTime.ForInput(entity.ValidFrom),
            ValidToLocal = VietnamTime.ForInput(entity.ValidTo),
            MaxUses = entity.MaxUses,
            IsActive = entity.IsActive,
            UsedCount = entity.UsedCount
        };
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveAsync(PromoCodeSaveRequest input, CancellationToken ct = default)
    {
        logger.LogInformation("SavePromoCode Start Id={Id}", input.Id);
        try
        {
            var code = PromoCodeRules.Normalize(input.Code);
            if (code is null || !PromoCodeRules.IsCodeShape(code))
                return Reject(PromoCodeRules.CodeRequired);
            if (!TryParseLocal(input.ValidFromLocal, out var fromUtc) || !TryParseLocal(input.ValidToLocal, out var toUtc))
                return Reject(PromoCodeRules.DateInvalid);

            var existing = input.Id is int id
                ? await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == id, ct)
                : null;
            if (input.Id is not null && existing is null)
                return Reject(PromoCodeRules.NotFound);

            var used = existing?.UsedCount ?? 0;
            var error = PromoCodeRules.ValidateDefinition(code, input.DiscountPercent, fromUtc, toUtc, input.MaxUses, used);
            if (error is not null)
                return Reject(error);
            if (existing is not null && existing.UsedCount > 0 && !string.Equals(existing.Code, code, StringComparison.Ordinal))
                return Reject(PromoCodeRules.CodeLocked);

            var takenId = existing?.Id ?? 0;
            var taken = await db.PromoCodes.AnyAsync(p => p.Code == code && p.Id != takenId, ct);
            if (taken)
                return Reject(PromoCodeRules.Duplicate);

            var entity = existing ?? new PromoCode { CreatedAt = DateTime.UtcNow };
            if (existing is null)
                db.PromoCodes.Add(entity);

            entity.Code = code;
            entity.DiscountPercent = input.DiscountPercent;
            entity.ValidFrom = fromUtc;
            entity.ValidTo = toUtc;
            entity.MaxUses = input.MaxUses;
            entity.IsActive = input.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("SavePromoCode Done Id={Id} Code={Code}", entity.Id, entity.Code);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SavePromoCode Error Id={Id}", input.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("DeletePromoCode Start Id={Id}", id);
        try
        {
            var entity = await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity is null)
            {
                logger.LogWarning("DeletePromoCode Done rejected Id={Id} Error={Error}", id, PromoCodeRules.NotFound);
                return (false, PromoCodeRules.NotFound);
            }
            if (entity.UsedCount > 0 || await db.Orders.AnyAsync(o => o.PromoCodeId == id, ct))
            {
                logger.LogWarning("DeletePromoCode Done rejected Id={Id} Error={Error}", id, PromoCodeRules.DeleteUsed);
                return (false, PromoCodeRules.DeleteUsed);
            }

            db.PromoCodes.Remove(entity);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeletePromoCode Done Id={Id} Code={Code}", id, entity.Code);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeletePromoCode Error Id={Id}", id);
            throw;
        }
    }

    private (bool Ok, string? Error, int? Id) Reject(string error)
    {
        logger.LogWarning("SavePromoCode Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private static bool TryParseLocal(string? text, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!DateTime.TryParseExact(text.Trim(), LocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return false;
        utc = VietnamTime.ToUtc(local);
        return true;
    }

    private static PromoCodeState ToState(PromoCode p) => new(
        p.Id, p.Code, p.DiscountPercent, p.ValidFrom, p.ValidTo, p.MaxUses, p.UsedCount, p.IsActive);
}
