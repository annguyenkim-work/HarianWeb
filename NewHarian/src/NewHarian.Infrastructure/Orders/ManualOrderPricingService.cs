using Microsoft.EntityFrameworkCore;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Application.Orders;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Orders;

public sealed class ManualOrderPricingService(AppDbContext db, IDealerDiscountResolver discounts) : IManualOrderPricingService
{
    public async Task<ManualOrderPricingResult> PriceAsync(
        int? dealerId,
        IReadOnlyList<ManualOrderLineRequest> lines,
        bool allowManualDiscount,
        CancellationToken ct = default)
    {
        var indexed = lines
            .Select((l, i) => (Line: l, Index: i))
            .Where(x => !string.IsNullOrWhiteSpace(x.Line.Sku))
            .ToList();

        var skuLower = indexed.Select(x => x.Line.Sku.Trim().ToLowerInvariant()).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p.Translations)
            .Where(v => v.IsActive && skuLower.Contains(v.Sku.ToLower()))
            .ToListAsync(ct);
        var bySku = variants
            .GroupBy(v => v.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var resolved = new List<(int Index, ManualOrderLineRequest Line, ProductVariant? Variant, string? Error)>();
        foreach (var (line, index) in indexed)
        {
            var sku = line.Sku.Trim();
            string? error = null;
            bySku.TryGetValue(sku, out var variant);
            if (line.Quantity <= 0)
                error = $"Dòng {index + 1}: số lượng phải > 0.";
            else if (line.UnitPrice is < 0)
                error = $"Dòng {index + 1}: đơn giá không hợp lệ.";
            else if (variant is null)
                error = $"Dòng {index + 1}: không tìm thấy SKU '{sku}'.";
            else if (dealerId is not null && allowManualDiscount && line.DiscountPercent is decimal p
                     && !DealerDiscountGroupValidator.IsPercent(p))
                error = $"Dòng {index + 1}: % chiết khấu phải từ 0 đến 100.";
            resolved.Add((index, line, error is null ? variant : null, error));
        }

        var valid = resolved.Where(r => r.Variant is not null).ToList();
        var inputs = valid.Select(r => new DiscountLineInput(
                r.Variant!.Id,
                r.Line.Quantity,
                (r.Line.UnitPrice ?? r.Variant.Price) * r.Line.Quantity,
                allowManualDiscount ? r.Line.DiscountPercent : null))
            .ToList();

        var discount = dealerId is int did
            ? DealerDiscountCalculator.Calculate(await discounts.GetPolicyAsync(did, ct), inputs)
            : null;

        var priced = new List<ManualOrderPricedLine>(resolved.Count);
        var validIdx = 0;
        foreach (var r in resolved)
        {
            if (r.Variant is null)
            {
                priced.Add(new ManualOrderPricedLine(
                    r.Index, r.Line.Sku.Trim(), r.Error, 0, 0, "", "", 0, r.Line.Quantity, 0,
                    0, 0, null, DealerDiscountSource.None, false, 0));
                continue;
            }

            var input = inputs[validIdx];
            var d = discount?.Lines[validIdx];
            validIdx++;
            var v = r.Variant;
            priced.Add(new ManualOrderPricedLine(
                r.Index,
                v.Sku,
                null,
                v.ProductId,
                v.Id,
                v.Product.Translations.OrderBy(t => t.LanguageCode == "vi" ? 0 : 1).Select(t => t.Name).FirstOrDefault()
                    ?? v.Product.Slug,
                v.VariantLabel,
                r.Line.UnitPrice ?? v.Price,
                r.Line.Quantity,
                input.LineTotal,
                d?.Percent ?? 0,
                d?.Amount ?? 0,
                d?.TierLabel,
                d?.Source ?? DealerDiscountSource.None,
                d?.Unconfigured ?? false,
                d?.PolicyPercent ?? 0));
        }

        return new ManualOrderPricingResult(
            priced,
            priced.Where(p => p.Error is null).Sum(p => p.LineTotal),
            priced.Where(p => p.Error is null).Sum(p => p.DiscountAmount));
    }
}
