using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Cart;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Cart;

public class SessionCartService(IHttpContextAccessor http, AppDbContext db, ILogger<SessionCartService> logger) : ICartService
{
    private const string SessionKey = "Cart";

    public CartDto GetCart()
    {
        var sessionItems = Load();
        if (sessionItems.Count == 0) return new CartDto([]);

        var ids = sessionItems.Select(i => i.ProductVariantId).ToList();
        var variants = db.ProductVariants.AsNoTracking()
            .Where(v => ids.Contains(v.Id) && v.IsActive)
            .Include(v => v.Image)
            .Include(v => v.ColorDefinition).ThenInclude(c => c!.Translations)
            .Include(v => v.Product).ThenInclude(p => p.Translations)
            .Include(v => v.Product).ThenInclude(p => p.MainImage)
            .ToList();

        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var items = new List<CartItemDto>();
        foreach (var s in sessionItems)
        {
            var v = variants.FirstOrDefault(x => x.Id == s.ProductVariantId);
            if (v is null || v.Product.Status != ProductStatus.Published)
                continue;
            var name = v.Product.Translations.FirstOrDefault(t => t.LanguageCode == lang)?.Name
                       ?? v.Product.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                       ?? v.Product.Slug;
            var img = v.Image?.StoredPath ?? v.Product.MainImage?.StoredPath;
            items.Add(new CartItemDto(v.Id, v.ProductId, name, BuildVariantLabel(v, lang), v.Sku, v.Price, s.Quantity, img));
        }
        return new CartDto(items);
    }

    private static string BuildVariantLabel(Domain.Entities.ProductVariant v, string lang)
    {
        var sizePart = v.Product.HasVariantSize ? (v.VariantLabel ?? "").Trim() : "";
        var colorName = "";
        if (v.Product.HasVariantColor && v.ColorDefinition is not null)
        {
            var tr = v.ColorDefinition.Translations.FirstOrDefault(t => t.LanguageCode == lang)
                     ?? v.ColorDefinition.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
            colorName = (tr?.Name ?? "").Trim();
        }

        var label = string.Join(" / ", new[] { sizePart, colorName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(label))
            label = (v.VariantLabel ?? "").Trim();
        return label;
    }

    public (bool Ok, string? Error) Add(int productVariantId, int quantity)
    {
        logger.LogInformation("CartAdd Start VariantId={VariantId} Qty={Qty}", productVariantId, quantity);
        try
        {
            if (quantity < 1)
            {
                logger.LogWarning("CartAdd Done rejected Error={Error}", "Số lượng phải ≥ 1.");
                return (false, "Số lượng phải ≥ 1.");
            }
            var v = db.ProductVariants.AsNoTracking()
                .Include(x => x.Product)
                .FirstOrDefault(x => x.Id == productVariantId && x.IsActive);
            if (v is null || v.Product.Status != ProductStatus.Published)
            {
                logger.LogWarning("CartAdd Done rejected VariantId={VariantId} Error={Error}", productVariantId, "Sản phẩm không hợp lệ.");
                return (false, "Sản phẩm không hợp lệ.");
            }

            var items = Load();
            var existing = items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
            if (existing is null)
                items.Add(new SessionCartItem(productVariantId, quantity));
            else
            {
                items.Remove(existing);
                items.Add(existing with { Quantity = existing.Quantity + quantity });
            }
            Save(items);
            logger.LogInformation("CartAdd Done VariantId={VariantId} Qty={Qty}", productVariantId, quantity);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CartAdd Error VariantId={VariantId}", productVariantId);
            throw;
        }
    }

    public (bool Ok, string? Error) Update(int productVariantId, int quantity)
    {
        logger.LogInformation("CartUpdate Start VariantId={VariantId} Qty={Qty}", productVariantId, quantity);
        try
        {
            if (quantity < 1)
            {
                logger.LogWarning("CartUpdate Done rejected Error={Error}", "Số lượng phải ≥ 1.");
                return (false, "Số lượng phải ≥ 1.");
            }
            var items = Load();
            var existing = items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
            if (existing is null)
            {
                logger.LogWarning("CartUpdate Done rejected VariantId={VariantId} Error={Error}", productVariantId, "Không có trong giỏ.");
                return (false, "Không có trong giỏ.");
            }
            items.Remove(existing);
            items.Add(existing with { Quantity = quantity });
            Save(items);
            logger.LogInformation("CartUpdate Done VariantId={VariantId} Qty={Qty}", productVariantId, quantity);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CartUpdate Error VariantId={VariantId}", productVariantId);
            throw;
        }
    }

    public void Remove(int productVariantId)
    {
        logger.LogInformation("CartRemove Start VariantId={VariantId}", productVariantId);
        try
        {
            var items = Load().Where(i => i.ProductVariantId != productVariantId).ToList();
            Save(items);
            logger.LogInformation("CartRemove Done VariantId={VariantId}", productVariantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CartRemove Error VariantId={VariantId}", productVariantId);
            throw;
        }
    }

    public void Clear()
    {
        logger.LogInformation("CartClear Start");
        try
        {
            Save([]);
            logger.LogInformation("CartClear Done");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CartClear Error");
            throw;
        }
    }

    private List<SessionCartItem> Load()
    {
        var json = http.HttpContext?.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json)) return [];
        return JsonSerializer.Deserialize<List<SessionCartItem>>(json) ?? [];
    }

    private void Save(List<SessionCartItem> items)
    {
        http.HttpContext?.Session.SetString(SessionKey, JsonSerializer.Serialize(items));
    }

    private record SessionCartItem(int ProductVariantId, int Quantity);
}
