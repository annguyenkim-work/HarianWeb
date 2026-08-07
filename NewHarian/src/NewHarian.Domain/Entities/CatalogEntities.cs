using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool ShowOnHome { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CategoryTranslation> Translations { get; set; } = new List<CategoryTranslation>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
}

public class CategoryTranslation
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Category Category { get; set; } = null!;
}

/// <summary>Physical goods sold via cart/checkout.</summary>
public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public bool HasVariantSize { get; set; }
    public bool HasVariantColor { get; set; }
    public int? MainImageMediaFileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
    public MediaFile? MainImage { get; set; }
    public ICollection<ProductTranslation> Translations { get; set; } = new List<ProductTranslation>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}

public class ProductTranslation
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Product Product { get; set; } = null!;
}

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public int? ColorDefinitionId { get; set; }
    public ColorDefinition? ColorDefinition { get; set; }
    public int? ImageMediaFileId { get; set; }
    public MediaFile? Image { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int? StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Product Product { get; set; } = null!;
}

/// <summary>Bookable services (separate table from Products).</summary>
public class Service
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public bool HasVariantSize { get; set; }
    public bool HasVariantColor { get; set; }
    /// <summary>When true, guest UI hides prices (quote / book-only).</summary>
    public bool HidePrice { get; set; }
    public int? MainImageMediaFileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
    public MediaFile? MainImage { get; set; }
    public ICollection<ServiceTranslation> Translations { get; set; } = new List<ServiceTranslation>();
    public ICollection<ServiceVariant> Variants { get; set; } = new List<ServiceVariant>();
    public ICollection<ServiceBooking> ServiceBookings { get; set; } = new List<ServiceBooking>();
}

public class ServiceTranslation
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Service Service { get; set; } = null!;
}

public class ServiceVariant
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public int? ColorDefinitionId { get; set; }
    public ColorDefinition? ColorDefinition { get; set; }
    public int? ImageMediaFileId { get; set; }
    public MediaFile? Image { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Service Service { get; set; } = null!;
}

public class ColorDefinition
{
    public int Id { get; set; }
    public ICollection<ColorDefinitionTranslation> Translations { get; set; } = new List<ColorDefinitionTranslation>();
}

public class ColorDefinitionTranslation
{
    public int Id { get; set; }
    public int ColorDefinitionId { get; set; }
    public string LanguageCode { get; set; } = "vi";

    public string Name { get; set; } = string.Empty;
    public string? Meaning { get; set; }

    public ColorDefinition ColorDefinition { get; set; } = null!;
}

public class Tag
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}

public class ProductTag
{
    public int ProductId { get; set; }
    public int TagId { get; set; }

    public Product Product { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public class ServiceBooking
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public int ServiceId { get; set; }
    public int ServiceVariantId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateOnly PreferredDate { get; set; }
    public string PreferredTime { get; set; } = string.Empty;
    public string? ServiceAddress { get; set; }
    public string? Notes { get; set; }
    public ServiceBookingStatus Status { get; set; } = ServiceBookingStatus.New;
    public string? InternalNotes { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }

    public Service Service { get; set; } = null!;
    public ServiceVariant ServiceVariant { get; set; } = null!;
    public ICollection<ServiceBookingHistory> Histories { get; set; } = new List<ServiceBookingHistory>();
}
