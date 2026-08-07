namespace NewHarian.Domain.Entities;

/// <summary>Admin-editable email subject + rich HTML body with {{Placeholder}} tokens.</summary>
public class EmailTemplate
{
    public int Id { get; set; }
    /// <summary>Stable key, e.g. booking.customer</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Short hint of available placeholders for Admin UI.</summary>
    public string PlaceholdersHelp { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
