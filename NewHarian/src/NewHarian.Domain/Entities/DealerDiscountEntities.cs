namespace NewHarian.Domain.Entities;

/// <summary>SKUs whose quantities are summed to pick a tier (e.g. all KIRARA CREAM variants).</summary>
public class DealerDiscountGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<DealerDiscountGroupVariant> Variants { get; set; } = new List<DealerDiscountGroupVariant>();
    public ICollection<DealerDiscountTier> Tiers { get; set; } = new List<DealerDiscountTier>();
}

public class DealerDiscountGroupVariant
{
    public int GroupId { get; set; }
    public int ProductVariantId { get; set; }

    public DealerDiscountGroup Group { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
}

public class DealerDiscountTier
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int MinQuantity { get; set; }
    /// <summary>Null = no upper bound.</summary>
    public int? MaxQuantity { get; set; }
    public int SortOrder { get; set; }

    public DealerDiscountGroup Group { get; set; } = null!;
    public ICollection<DealerDiscountRate> Rates { get; set; } = new List<DealerDiscountRate>();
}

public class DealerDiscountRate
{
    public int TierId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal Percent { get; set; }

    public DealerDiscountTier Tier { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
}

public class DealerDiscountOverride
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public int TierId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal Percent { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public Dealer Dealer { get; set; } = null!;
    public DealerDiscountTier Tier { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
}
