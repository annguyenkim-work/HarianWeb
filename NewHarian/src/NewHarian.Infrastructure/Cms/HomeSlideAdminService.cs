using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Cms;

public sealed class HomeSlideAdminService(
    AppDbContext db,
    IMediaStorage media,
    ILogger<HomeSlideAdminService> logger) : IHomeSlideAdminService
{
    public async Task<IReadOnlyList<HomeSlide>> ListAsync(CancellationToken ct = default)
        => await db.HomeSlides.AsNoTracking()
            .Include(s => s.Translations)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

    public async Task CreateAsync(
        string? captionVi,
        string? linkUrl,
        bool isActive,
        HomeSlideImageUpload? image,
        CancellationToken ct = default)
    {
        logger.LogInformation("CreateHomeSlide Start");
        try
        {
            string? imageUrl = null;
            int? mediaId = null;
            if (image is not null)
            {
                var uploaded = await media.SaveImageAsync(
                    image.Content, image.FileName, image.ContentType, image.UploadedByUserId, ct, "slides");
                imageUrl = uploaded.Url;
                mediaId = uploaded.Id;
            }

            var maxOrder = await db.HomeSlides.MaxAsync(s => (int?)s.SortOrder, ct) ?? 0;
            db.HomeSlides.Add(new HomeSlide
            {
                ImageUrl = imageUrl,
                MediaFileId = mediaId,
                LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
                SortOrder = maxOrder + 1,
                IsActive = isActive,
                Translations = { new HomeSlideTranslation { LanguageCode = "vi", Caption = captionVi } }
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("CreateHomeSlide Done MediaId={MediaId}", mediaId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateHomeSlide Error");
            throw;
        }
    }

    public async Task MoveAsync(int id, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("MoveHomeSlide Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var slides = await db.HomeSlides.OrderBy(s => s.SortOrder).ToListAsync(ct);
            var index = slides.FindIndex(s => s.Id == id);
            var swapIndex = index + direction;
            if (index >= 0 && swapIndex >= 0 && swapIndex < slides.Count)
            {
                (slides[index].SortOrder, slides[swapIndex].SortOrder) =
                    (slides[swapIndex].SortOrder, slides[index].SortOrder);
                await db.SaveChangesAsync(ct);
            }
            logger.LogInformation("MoveHomeSlide Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveHomeSlide Error Id={Id}", id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteHomeSlide Start Id={Id}", id);
        try
        {
            var slide = await db.HomeSlides.FindAsync([id], ct);
            if (slide is null)
            {
                logger.LogWarning("DeleteHomeSlide Done rejected Id={Id}", id);
                return;
            }
            db.HomeSlides.Remove(slide);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeleteHomeSlide Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteHomeSlide Error Id={Id}", id);
            throw;
        }
    }
}
