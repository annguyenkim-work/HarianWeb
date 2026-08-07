using Microsoft.Extensions.Caching.Memory;

namespace NewHarian.Web.Areas.Admin.Services;

public interface IProductPreviewStore
{
    string Save(ProductPreviewSnapshot snapshot);
    ProductPreviewSnapshot? Get(string token);
}

public sealed class ProductPreviewStore(IMemoryCache cache) : IProductPreviewStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public string Save(ProductPreviewSnapshot snapshot)
    {
        var token = Guid.NewGuid().ToString("N");
        cache.Set(CacheKey(token), snapshot, Ttl);
        return token;
    }

    public ProductPreviewSnapshot? Get(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return cache.TryGetValue(CacheKey(token), out ProductPreviewSnapshot? snap) ? snap : null;
    }

    private static string CacheKey(string token) => $"product-preview:{token}";
}
