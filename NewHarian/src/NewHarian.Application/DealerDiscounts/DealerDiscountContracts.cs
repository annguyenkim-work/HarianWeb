namespace NewHarian.Application.DealerDiscounts;

public class DealerDiscountGroupInput
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public List<int> ProductVariantIds { get; set; } = new();
    public List<DealerDiscountTierInput> Tiers { get; set; } = new();
}

public class DealerDiscountTierInput
{
    /// <summary>Existing tier id; kept when the range changes so dealer overrides stay attached.</summary>
    public int? Id { get; set; }
    public int MinQuantity { get; set; }
    public int? MaxQuantity { get; set; }
    /// <summary>Key = ProductVariantId.</summary>
    public Dictionary<int, decimal?> Percents { get; set; } = new();
}

public record DealerDiscountSkuDto(int ProductVariantId, string Label, string Sku);

public record DealerDiscountTierDto(int Id, int MinQuantity, int? MaxQuantity)
{
    public string Range => DealerDiscountCalculator.TierRange(MinQuantity, MaxQuantity);
}

public record DealerDiscountGroupDto(
    int Id,
    string Name,
    IReadOnlyList<DealerDiscountTierDto> Tiers,
    IReadOnlyList<DealerDiscountSkuDto> Skus,
    IReadOnlyDictionary<(int TierId, int ProductVariantId), decimal> Rates);

public record DealerDiscountOverviewDto(
    IReadOnlyList<DealerDiscountGroupDto> Groups,
    IReadOnlyList<DealerDiscountSkuDto> UnconfiguredSkus);

/// <summary>SKU choice in the group modal; <see cref="GroupName"/> set when it already belongs to another group.</summary>
public record DealerDiscountSkuOptionDto(int ProductVariantId, string Label, string Sku, string? GroupName);

public class DealerDiscountGroupEditModel
{
    public DealerDiscountGroupInput Input { get; set; } = new();
    public IReadOnlyList<DealerDiscountSkuOptionDto> SkuOptions { get; set; } = [];
    public int DealersWithOverrides { get; set; }
}

public record DealerDiscountOverridesDto(
    int DealerId,
    string DealerName,
    IReadOnlyList<DealerDiscountGroupDto> Groups,
    IReadOnlyDictionary<(int TierId, int ProductVariantId), decimal> Overrides);

public class DealerDiscountOverrideCell
{
    public int TierId { get; set; }
    public int ProductVariantId { get; set; }
    /// <summary>Null = follow the default table.</summary>
    public decimal? Percent { get; set; }
}

public class DealerDiscountOverridesInput
{
    public int DealerId { get; set; }
    public List<DealerDiscountOverrideCell> Cells { get; set; } = new();
}

/// <summary>Default discount table (groups / tiers / rates).</summary>
public interface IAdminDealerDiscountService
{
    Task<DealerDiscountOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<DealerDiscountGroupEditModel> GetGroupEditAsync(int? id, int? preselectVariantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<DealerDiscountSkuOptionDto>> ListSkuOptionsAsync(int? groupId, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveGroupAsync(DealerDiscountGroupInput input, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteGroupAsync(int id, string? userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> MoveGroupAsync(int id, int direction, CancellationToken ct = default);
}

/// <summary>Per-dealer override cells on top of the default table.</summary>
public interface IDealerDiscountOverrideService
{
    Task<DealerDiscountOverridesDto?> GetAsync(int dealerId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SaveAsync(DealerDiscountOverridesInput input, string? userId, CancellationToken ct = default);
}

/// <summary>Read side used when pricing a dealer order.</summary>
public interface IDealerDiscountResolver
{
    Task<DealerDiscountPolicy> GetPolicyAsync(int dealerId, CancellationToken ct = default);
}
