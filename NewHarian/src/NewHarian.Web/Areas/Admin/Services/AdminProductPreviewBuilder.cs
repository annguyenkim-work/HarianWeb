using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Catalog;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Services;

public interface IAdminProductPreviewBuilder
{
    Task<(bool Ok, string? Error, ProductPreviewSnapshot? Snapshot)> BuildAsync(
        ProductSaveRequest model,
        CancellationToken ct = default);
}

public sealed class AdminProductPreviewBuilder(AppDbContext db) : IAdminProductPreviewBuilder
{
    public async Task<(bool Ok, string? Error, ProductPreviewSnapshot? Snapshot)> BuildAsync(
        ProductSaveRequest model,
        CancellationToken ct = default)
    {
        model.Variants ??= [];
        if (model.CategoryId <= 0)
            return (false, "Chọn danh mục trước khi xem trước.", null);
        if (string.IsNullOrWhiteSpace(model.NameVi))
            return (false, "Nhập tên tiếng Việt để xem trước.", null);

        var category = await db.Categories.AsNoTracking()
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == model.CategoryId, ct);
        if (category is null)
            return (false, "Danh mục không hợp lệ.", null);

        await AdminProductFormHelper.ResolvePreviewImageUrlsAsync(db, model, ct);

        var colorIds = model.Variants
            .Where(v => v.ColorDefinitionId.HasValue)
            .Select(v => v.ColorDefinitionId!.Value)
            .Distinct()
            .ToList();
        var colors = new Dictionary<int, IReadOnlyList<ColorTranslationSnapshot>>();
        if (colorIds.Count > 0)
        {
            var colorEntities = await db.ColorDefinitions.AsNoTracking()
                .Include(c => c.Translations)
                .Where(c => colorIds.Contains(c.Id))
                .ToListAsync(ct);
            foreach (var c in colorEntities)
            {
                colors[c.Id] = c.Translations
                    .Select(t => new ColorTranslationSnapshot(t.LanguageCode, t.Name, t.Meaning))
                    .ToList();
            }
        }

        var categoryNames = category.Translations
            .GroupBy(t => t.LanguageCode)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return (true, null, new ProductPreviewSnapshot
        {
            Request = model,
            CategorySlug = category.Slug,
            CategoryNames = categoryNames,
            Colors = colors
        });
    }
}
