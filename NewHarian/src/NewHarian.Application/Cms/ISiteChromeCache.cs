namespace NewHarian.Application.Cms;

/// <summary>Memory cache for guest chrome (settings + header menu). Invalidate on admin save.</summary>
public interface ISiteChromeCache
{
    Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync(CancellationToken ct = default);
    void InvalidateSettings();
    void InvalidateMenus();
}
