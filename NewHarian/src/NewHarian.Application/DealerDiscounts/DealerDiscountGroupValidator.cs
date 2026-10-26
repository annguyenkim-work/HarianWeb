using NewHarian.Application.Validation;

namespace NewHarian.Application.DealerDiscounts;

public static class DealerDiscountGroupValidator
{
    public const int NameMax = 200;

    public static bool IsPercent(decimal p) => p is >= 0 and <= 100 && decimal.Round(p, 2) == p;

    /// <summary>Returns the first validation error (VI) or null.</summary>
    public static string? Validate(DealerDiscountGroupInput g)
    {
        if (!GuestValidation.HasLength(g.Name, 2, NameMax))
            return "Tên nhóm phải từ 2-200 ký tự.";

        var variants = g.ProductVariantIds ?? [];
        if (variants.Count == 0)
            return "Chọn ít nhất 1 SKU.";
        if (variants.Distinct().Count() != variants.Count)
            return "SKU bị chọn trùng.";

        var tiers = g.Tiers ?? [];
        if (tiers.Count == 0)
            return "Cần ít nhất 1 mức số lượng.";
        if (tiers[0].MinQuantity != 1)
            return "Mức đầu tiên phải bắt đầu từ 1.";

        for (var i = 0; i < tiers.Count; i++)
        {
            var t = tiers[i];
            var isLast = i == tiers.Count - 1;
            if (i > 0)
            {
                var prevMax = tiers[i - 1].MaxQuantity;
                if (prevMax is null || t.MinQuantity != prevMax + 1)
                    return $"Mức {i + 1}: 'Từ' phải bằng 'Đến' của mức trước + 1.";
            }
            if (isLast && t.MaxQuantity is not null)
                return "Mức cuối phải để trống 'Đến' (trở lên).";
            if (!isLast && t.MaxQuantity is null)
                return $"Mức {i + 1}: chỉ mức cuối được để trống 'Đến'.";
            if (t.MaxQuantity is int max && max < t.MinQuantity)
                return $"Mức {i + 1}: 'Đến' phải lớn hơn hoặc bằng 'Từ'.";

            foreach (var vid in variants)
            {
                if (t.Percents is null || !t.Percents.TryGetValue(vid, out var p) || p is null)
                    return $"Mức {i + 1}: nhập đủ % cho mọi SKU.";
                if (!IsPercent(p.Value))
                    return $"Mức {i + 1}: % phải từ 0 đến 100, tối đa 2 chữ số thập phân.";
            }
        }

        return null;
    }
}
