using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewHarian.Application.Cms;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Cms;

public sealed class SiteChromeCache(AppDbContext db, IMemoryCache cache) : ISiteChromeCache
{
    public const string SettingsKey = "chrome.settings";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static string HeaderNavKey(string lang)
        => $"chrome.nav.{(lang is "en" or "ja" ? lang : "vi")}";

    public async Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync(CancellationToken ct = default)
    {
        var map = await cache.GetOrCreateAsync(SettingsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return await db.SiteSettings.AsNoTracking()
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        });
        return map ?? new Dictionary<string, string?>();
    }

    public void InvalidateSettings() => cache.Remove(SettingsKey);

    public void InvalidateMenus()
    {
        cache.Remove(HeaderNavKey("vi"));
        cache.Remove(HeaderNavKey("en"));
        cache.Remove(HeaderNavKey("ja"));
    }
}
