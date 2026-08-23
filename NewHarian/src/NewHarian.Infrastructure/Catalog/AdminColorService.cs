using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class AdminColorService(AppDbContext db, ILogger<AdminColorService> logger) : IAdminColorService
{
    public async Task<IReadOnlyList<AdminColorListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await db.ColorDefinitions.AsNoTracking()
            .Include(c => c.Translations)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        return list.Select(c => new AdminColorListItemDto(
            c.Id,
            PickName(c.Translations, "vi") ?? $"Color #{c.Id}",
            PickMeaning(c.Translations, "vi"))).ToList();
    }

    public async Task<ColorDefinitionSaveRequest?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ColorDefinitions.AsNoTracking()
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return null;

        string? Pick(string lang, Func<ColorDefinitionTranslation, string?> getter)
            => entity.Translations.FirstOrDefault(t => t.LanguageCode == lang) is { } tr ? getter(tr) : null;

        return new ColorDefinitionSaveRequest
        {
            Id = entity.Id,
            NameVi = Pick("vi", t => t.Name) ?? "",
            MeaningVi = Pick("vi", t => t.Meaning),
            NameEn = Pick("en", t => t.Name) ?? "",
            MeaningEn = Pick("en", t => t.Meaning),
            NameJa = Pick("ja", t => t.Name) ?? "",
            MeaningJa = Pick("ja", t => t.Meaning),
        };
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveAsync(ColorDefinitionSaveRequest model, CancellationToken ct = default)
    {
        logger.LogInformation("SaveColor Start Id={Id}", model.Id);
        try
        {
            if (string.IsNullOrWhiteSpace(model.NameVi))
            {
                logger.LogWarning("SaveColor Done rejected Error={Error}", "Tên màu (VI) bắt buộc.");
                return (false, "Tên màu (VI) bắt buộc.", null);
            }

            ColorDefinition entity;
            if (model.Id is int id)
            {
                entity = await db.ColorDefinitions.Include(c => c.Translations)
                    .FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new InvalidOperationException("ColorDefinition not found");
            }
            else
            {
                entity = new ColorDefinition();
                db.ColorDefinitions.Add(entity);
            }

            var nameVi = model.NameVi.Trim();
            var nameEn = string.IsNullOrWhiteSpace(model.NameEn) ? nameVi : model.NameEn.Trim();
            var nameJa = string.IsNullOrWhiteSpace(model.NameJa) ? nameVi : model.NameJa.Trim();

            Upsert(entity, "vi", nameVi, model.MeaningVi);
            Upsert(entity, "en", nameEn, model.MeaningEn);
            Upsert(entity, "ja", nameJa, model.MeaningJa);

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveColor Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveColor Error Id={Id}", model.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteColor Start Id={Id}", id);
        try
        {
            var entity = await db.ColorDefinitions.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (entity is not null)
            {
                db.ColorDefinitions.Remove(entity);
                await db.SaveChangesAsync(ct);
            }
            logger.LogInformation("DeleteColor Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteColor Error Id={Id}", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<AdminColorDefinitionOptionDto>> GetOptionsAsync(CancellationToken ct = default)
    {
        var colors = await db.ColorDefinitions.AsNoTracking()
            .Include(c => c.Translations)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        return colors.Select(c => new AdminColorDefinitionOptionDto(
                c.Id,
                PickName(c.Translations, "vi") ?? $"Color #{c.Id}"))
            .ToList();
    }

    private static void Upsert(ColorDefinition entity, string lang, string name, string? meaning)
    {
        var t = entity.Translations.FirstOrDefault(x => x.LanguageCode == lang);
        if (t is null)
        {
            t = new ColorDefinitionTranslation { LanguageCode = lang };
            entity.Translations.Add(t);
        }
        t.Name = name;
        t.Meaning = meaning;
    }

    private static string? PickMeaning(IEnumerable<ColorDefinitionTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang)?.Meaning
           ?? t.FirstOrDefault(x => x.LanguageCode == "vi")?.Meaning
           ?? t.FirstOrDefault()?.Meaning;

    private static string? PickName(IEnumerable<ColorDefinitionTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang)?.Name
           ?? t.FirstOrDefault(x => x.LanguageCode == "vi")?.Name
           ?? t.FirstOrDefault()?.Name;
}
