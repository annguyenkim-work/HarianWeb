namespace NewHarian.Application.PromoCodes;

public sealed class PromoCodeSaveRequest
{
    public int? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public string ValidFromLocal { get; set; } = string.Empty;
    public string ValidToLocal { get; set; } = string.Empty;
    public int MaxUses { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    /// <summary>Read-only on the form. The service trusts the stored count, not this value.</summary>
    public int UsedCount { get; set; }
}

public sealed record PromoCodeListItemDto(
    int Id,
    string Code,
    decimal DiscountPercent,
    string ValidFromText,
    string ValidToText,
    int MaxUses,
    int UsedCount,
    bool IsActive,
    string StatusLabel);

public interface IAdminPromoCodeService
{
    Task<IReadOnlyList<PromoCodeListItemDto>> ListAsync(CancellationToken ct = default);
    Task<PromoCodeSaveRequest> NewAsync();
    Task<PromoCodeSaveRequest?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveAsync(PromoCodeSaveRequest input, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IPromoCodeService
{
    Task<PromoQuote> QuoteAsync(string? code, decimal goodsSubtotal, bool isDealer, CancellationToken ct = default);

    /// <summary>Increments the use count on the tracked entity. The caller saves together with the order.</summary>
    Task<PromoQuote> TryConsumeAsync(string? code, decimal goodsSubtotal, bool isDealer, CancellationToken ct = default);

    /// <summary>Returns one use when an order that consumed the code is cancelled. The caller saves.</summary>
    Task ReleaseAsync(int? promoCodeId, CancellationToken ct = default);
}
