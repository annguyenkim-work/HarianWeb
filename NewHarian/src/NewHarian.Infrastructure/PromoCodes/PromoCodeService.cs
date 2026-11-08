using Microsoft.EntityFrameworkCore;
using NewHarian.Application.PromoCodes;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.PromoCodes;

public sealed class PromoCodeService(AppDbContext db) : IPromoCodeService
{
    public Task<PromoQuote> QuoteAsync(string? code, decimal goodsSubtotal, bool isDealer, CancellationToken ct = default)
        => EvaluateAsync(code, goodsSubtotal, isDealer, track: false, ct);

    public async Task<PromoQuote> TryConsumeAsync(string? code, decimal goodsSubtotal, bool isDealer, CancellationToken ct = default)
    {
        var quote = await EvaluateAsync(code, goodsSubtotal, isDealer, track: true, ct);
        if (!quote.HasDiscount || quote.PromoCodeId is not int id) return quote;

        var promo = await db.PromoCodes.FirstAsync(p => p.Id == id, ct);
        var again = PromoCodeRules.Evaluate(
            PromoCodeRules.Normalize(promo.Code), ToState(promo), DateTime.UtcNow, goodsSubtotal, isDealer);
        if (!again.HasDiscount) return again;
        promo.UsedCount++;
        return again;
    }

    public async Task ReleaseAsync(int? promoCodeId, CancellationToken ct = default)
    {
        if (promoCodeId is not int id) return;
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null || promo.UsedCount <= 0) return;
        promo.UsedCount--;
    }

    private async Task<PromoQuote> EvaluateAsync(
        string? code, decimal goodsSubtotal, bool isDealer, bool track, CancellationToken ct)
    {
        var normalized = PromoCodeRules.Normalize(code);
        if (normalized is null) return PromoQuote.None;
        if (isDealer) return PromoQuote.Fail(PromoCodeRules.RetailOnly);
        if (!PromoCodeRules.IsCodeShape(normalized)) return PromoQuote.Fail(PromoCodeRules.Invalid);

        var query = db.PromoCodes.AsQueryable();
        if (!track) query = query.AsNoTracking();
        var promo = await query.FirstOrDefaultAsync(p => p.Code == normalized, ct);
        return PromoCodeRules.Evaluate(normalized, promo is null ? null : ToState(promo), DateTime.UtcNow, goodsSubtotal, isDealer);
    }

    private static PromoCodeState ToState(PromoCode p) => new(
        p.Id, p.Code, p.DiscountPercent, p.ValidFrom, p.ValidTo, p.MaxUses, p.UsedCount, p.IsActive);
}
