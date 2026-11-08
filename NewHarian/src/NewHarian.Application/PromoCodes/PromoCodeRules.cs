namespace NewHarian.Application.PromoCodes;

public sealed record PromoCodeState(
    int Id,
    string Code,
    decimal DiscountPercent,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    int MaxUses,
    int UsedCount,
    bool IsActive);

public sealed record PromoQuote(bool Ok, string? Error, int? PromoCodeId, string? Code, decimal Percent, decimal Amount)
{
    public static PromoQuote None { get; } = new(true, null, null, null, 0, 0);

    public bool HasDiscount => Ok && Amount > 0;

    public static PromoQuote Fail(string error) => new(false, error, null, null, 0, 0);

    public static PromoQuote Applied(int id, string code, decimal percent, decimal amount)
        => new(true, null, id, code, percent, amount);
}

/// <summary>Pure checks for saving a code and for applying it to a retail goods subtotal.</summary>
public static class PromoCodeRules
{
    public const int CodeMinLength = 3;
    public const int CodeMaxLength = 32;

    public const string RetailOnly = "Mã giảm giá chỉ áp dụng cho khách lẻ.";
    public const string Invalid = "Mã giảm giá không hợp lệ.";
    public const string Inactive = "Mã giảm giá đang tạm tắt.";
    public const string NotStarted = "Mã giảm giá chưa tới thời gian hiệu lực.";
    public const string Expired = "Mã giảm giá đã hết hạn.";
    public const string Exhausted = "Mã giảm giá đã hết số lượng.";
    public const string NoGoods = "Đơn không có giá trị hàng để giảm.";
    public const string TooSmall = "Giá trị đơn chưa đủ để áp dụng mức giảm này.";
    public const string CodeRequired = "Nhập mã (3-32 ký tự, chữ, số hoặc dấu gạch ngang).";
    public const string PercentInvalid = "Phần trăm giảm phải từ 0.01 đến 100.";
    public const string PercentScale = "Phần trăm giảm tối đa 2 chữ số thập phân.";
    public const string DateOrder = "Thời gian kết thúc phải sau thời gian bắt đầu.";
    public const string DateInvalid = "Thời gian hiệu lực không hợp lệ.";
    public const string QuantityInvalid = "Số lượng mã phải từ 1 trở lên.";
    public const string QuantityBelowUsed = "Số lượng không được nhỏ hơn số lần đã dùng.";
    public const string Duplicate = "Mã đã tồn tại.";
    public const string CodeLocked = "Mã đã được sử dụng, không đổi được mã.";
    public const string DeleteUsed = "Mã đã được sử dụng, hãy tắt thay vì xóa.";
    public const string NotFound = "Không tìm thấy mã giảm giá.";

    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var compact = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }

    public static bool IsCodeShape(string code)
        => code.Length is >= CodeMinLength and <= CodeMaxLength
           && code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public static string? ValidateDefinition(
        string code, decimal percent, DateTime validFromUtc, DateTime validToUtc, int maxUses, int usedCount)
    {
        if (!IsCodeShape(code)) return CodeRequired;
        if (percent < 0.01m || percent > 100m) return PercentInvalid;
        if (percent != decimal.Round(percent, 2)) return PercentScale;
        if (validToUtc <= validFromUtc) return DateOrder;
        if (maxUses < 1) return QuantityInvalid;
        if (maxUses < usedCount) return QuantityBelowUsed;
        return null;
    }

    /// <summary>Blank code is not an error. A dealer session never receives a discount.</summary>
    public static PromoQuote Evaluate(string? normalizedCode, PromoCodeState? state, DateTime utcNow, decimal goodsSubtotal, bool isDealer)
    {
        if (normalizedCode is null) return PromoQuote.None;
        if (isDealer) return PromoQuote.Fail(RetailOnly);
        if (!IsCodeShape(normalizedCode) || state is null || !string.Equals(state.Code, normalizedCode, StringComparison.Ordinal))
            return PromoQuote.Fail(Invalid);
        if (!state.IsActive) return PromoQuote.Fail(Inactive);
        if (utcNow < state.ValidFromUtc) return PromoQuote.Fail(NotStarted);
        if (utcNow > state.ValidToUtc) return PromoQuote.Fail(Expired);
        if (state.UsedCount >= state.MaxUses) return PromoQuote.Fail(Exhausted);
        if (goodsSubtotal <= 0) return PromoQuote.Fail(NoGoods);

        var amount = Math.Round(goodsSubtotal * state.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
        if (amount <= 0) return PromoQuote.Fail(TooSmall);
        if (amount > goodsSubtotal) amount = goodsSubtotal;
        return PromoQuote.Applied(state.Id, state.Code, state.DiscountPercent, amount);
    }

    public static string StatusLabel(PromoCodeState state, DateTime utcNow)
    {
        if (!state.IsActive) return "Tắt";
        if (utcNow < state.ValidFromUtc) return "Chưa hiệu lực";
        if (utcNow > state.ValidToUtc) return "Hết hạn";
        if (state.UsedCount >= state.MaxUses) return "Hết lượt";
        return "Đang dùng";
    }
}
