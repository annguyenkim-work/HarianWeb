using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class Page
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public int TemplateType { get; set; }
    public string? HeroImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PageTranslation> Translations { get; set; } = new List<PageTranslation>();
    public ICollection<ContentBlock> ContentBlocks { get; set; } = new List<ContentBlock>();
}

public class PageTranslation
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Title { get; set; } = string.Empty;
    public string? HeroTitle { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Page Page { get; set; } = null!;
}

public class ContentBlock
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public ContentBlockType BlockType { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public int? MediaFileId { get; set; }
    public string? LinkUrl { get; set; }
    public string? ExtraData { get; set; }
    public string? ImagePosition { get; set; }
    /// <summary>Margin below this block toward the next one, in rem (≥ 0).</summary>
    public decimal SpacingAfterRem { get; set; } = 0.35m;

    public Page Page { get; set; } = null!;
    public MediaFile? MediaFile { get; set; }
    public ICollection<ContentBlockTranslation> Translations { get; set; } = new List<ContentBlockTranslation>();
}

public class ContentBlockTranslation
{
    public int Id { get; set; }
    public int ContentBlockId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? Title { get; set; }
    public string? Body { get; set; }

    public ContentBlock ContentBlock { get; set; } = null!;
}

public class Menu
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}

public class MenuItem
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public int? ParentId { get; set; }
    /// <summary>Stable catalog key (home, products, about, …). Empty for legacy items.</summary>
    public string ItemKey { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Menu Menu { get; set; } = null!;
    public MenuItem? Parent { get; set; }
    public ICollection<MenuItem> Children { get; set; } = new List<MenuItem>();
    public ICollection<MenuItemTranslation> Translations { get; set; } = new List<MenuItemTranslation>();
}

public class MenuItemTranslation
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Label { get; set; } = string.Empty;

    public MenuItem MenuItem { get; set; } = null!;
}

public class HomeSlide
{
    public int Id { get; set; }
    public int? MediaFileId { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public MediaFile? MediaFile { get; set; }
    public ICollection<HomeSlideTranslation> Translations { get; set; } = new List<HomeSlideTranslation>();
}

public class HomeSlideTranslation
{
    public int Id { get; set; }
    public int HomeSlideId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? Caption { get; set; }

    public HomeSlide HomeSlide { get; set; } = null!;
}

public class SiteSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Group { get; set; } = "company";

    public ICollection<SiteSettingTranslation> Translations { get; set; } = new List<SiteSettingTranslation>();
}

public class SiteSettingTranslation
{
    public int Id { get; set; }
    public int SiteSettingId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? Value { get; set; }

    public SiteSetting SiteSetting { get; set; } = null!;
}
