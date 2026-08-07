using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class SitePost
{
    public int Id { get; set; }
    public PostKind Kind { get; set; }
    public string Slug { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int SortOrder { get; set; }
    public int? CoverImageMediaFileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public MediaFile? CoverImage { get; set; }
    public ICollection<SitePostTranslation> Translations { get; set; } = new List<SitePostTranslation>();
    public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
}

public class SitePostTranslation
{
    public int Id { get; set; }
    public int SitePostId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string Title { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? Body { get; set; }

    public SitePost SitePost { get; set; } = null!;
}
