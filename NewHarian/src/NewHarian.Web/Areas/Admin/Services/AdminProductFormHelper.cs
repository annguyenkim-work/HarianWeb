using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Catalog;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Services;

/// <summary>Shared mapping helpers for Admin Products / ServiceProducts (same DB table, separate controllers).</summary>
public static class AdminProductFormHelper
{
    public static ProductSaveRequest ToSaveRequest(AdminProductEditDto p) => new()
    {
        Id = p.Id,
        CategoryId = p.CategoryId,
        Slug = p.Slug,
        Kind = p.Kind,
        Status = p.Status,
        IsFeatured = p.IsFeatured,
        SortOrder = p.SortOrder,
        NameVi = p.NameVi,
        ShortVi = p.ShortVi,
        DescVi = p.DescVi,
        NameEn = p.NameEn,
        ShortEn = p.ShortEn,
        DescEn = p.DescEn,
        NameJa = p.NameJa,
        ShortJa = p.ShortJa,
        DescJa = p.DescJa,
        HasVariantSize = p.HasVariantSize,
        HasVariantColor = p.HasVariantColor,
        HidePrice = p.HidePrice,
        MainImageMediaFileId = p.MainImageMediaFileId,
        MainImageUrl = p.MainImageUrl,
        TagsCsv = p.TagsCsv,
        Variants = p.Variants.Select(v => new VariantSaveRequest
        {
            Id = v.Id,
            Sku = v.Sku,
            VariantLabel = v.VariantLabel,
            ColorDefinitionId = v.ColorDefinitionId,
            Price = v.Price,
            IsDefault = v.IsDefault,
            SortOrder = v.SortOrder,
            IsActive = v.IsActive,
            ImageMediaFileId = v.ImageMediaFileId,
            ImageUrl = v.ImageUrl
        }).ToList()
    };

    public static async Task ResolvePreviewImageUrlsAsync(AppDbContext db, ProductSaveRequest model, CancellationToken ct)
    {
        var mediaIds = new List<int>();
        if (model.MainImageMediaFileId is int mainId and > 0 && string.IsNullOrWhiteSpace(model.MainImageUrl))
            mediaIds.Add(mainId);
        foreach (var v in model.Variants)
        {
            if (v.ImageMediaFileId is int vid and > 0 && string.IsNullOrWhiteSpace(v.ImageUrl))
                mediaIds.Add(vid);
        }
        mediaIds = mediaIds.Distinct().ToList();
        if (mediaIds.Count == 0) return;

        var paths = await db.MediaFiles.AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.StoredPath, ct);

        if (model.MainImageMediaFileId is int mid && string.IsNullOrWhiteSpace(model.MainImageUrl)
            && paths.TryGetValue(mid, out var mainPath))
            model.MainImageUrl = mainPath;

        foreach (var v in model.Variants)
        {
            if (v.ImageMediaFileId is int id && string.IsNullOrWhiteSpace(v.ImageUrl)
                && paths.TryGetValue(id, out var path))
                v.ImageUrl = path;
        }
    }
}
