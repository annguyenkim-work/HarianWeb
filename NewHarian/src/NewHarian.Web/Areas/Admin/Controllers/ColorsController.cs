using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ColorsController(AppDbContext db, ILogger<ColorsController> logger) : Controller
{
    private static string? PickMeaning(IEnumerable<ColorDefinitionTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang)?.Meaning
           ?? t.FirstOrDefault(x => x.LanguageCode == "vi")?.Meaning
           ?? t.FirstOrDefault()?.Meaning;

    private static string? PickName(IEnumerable<ColorDefinitionTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang)?.Name
           ?? t.FirstOrDefault(x => x.LanguageCode == "vi")?.Name
           ?? t.FirstOrDefault()?.Name;

    public record ColorListItemDto(int Id, string NameVi, string? MeaningVi);

    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var list = await db.ColorDefinitions.AsNoTracking()
            .Include(c => c.Translations)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        var mapped = list.Select(c => new ColorListItemDto(
            c.Id,
            PickName(c.Translations, "vi") ?? $"Color #{c.Id}",
            PickMeaning(c.Translations, "vi")
        )).ToList();

        var (items, pager) = AdminPaging.Apply(mapped, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return PartialView("_ColorForm", new ColorDefinitionSaveRequest());

        var entity = await db.ColorDefinitions
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id.Value, ct);

        if (entity is null) return NotFound();

        string? Pick(string lang, Func<ColorDefinitionTranslation, string?> getter)
            => entity.Translations.FirstOrDefault(t => t.LanguageCode == lang) is { } tr ? getter(tr) : null;

        return PartialView("_ColorForm", new ColorDefinitionSaveRequest
        {
            Id = entity.Id,
            NameVi = Pick("vi", t => t.Name) ?? "",
            MeaningVi = Pick("vi", t => t.Meaning),
            NameEn = Pick("en", t => t.Name) ?? "",
            MeaningEn = Pick("en", t => t.Meaning),
            NameJa = Pick("ja", t => t.Name) ?? "",
            MeaningJa = Pick("ja", t => t.Meaning),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ColorDefinitionSaveRequest model, CancellationToken ct)
    {
        logger.LogInformation("SaveColor Start Id={Id}", model.Id);
        try
        {
            if (string.IsNullOrWhiteSpace(model.NameVi))
            {
                logger.LogWarning("SaveColor Done rejected Error={Error}", "Tên màu (VI) bắt buộc.");
                ModelState.AddModelError(string.Empty, "Tên màu (VI) bắt buộc.");
                return PartialView("_ColorForm", model);
            }

            ColorDefinition entity;
            if (model.Id is int id)
            {
                entity = await db.ColorDefinitions.Include(c => c.Translations)
                    .FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new InvalidOperationException("ColorDefinition not found");
            }
            else
            {
                entity = new ColorDefinition();
                db.ColorDefinitions.Add(entity);
            }

            var nameVi = model.NameVi.Trim();
            var nameEn = string.IsNullOrWhiteSpace(model.NameEn) ? nameVi : model.NameEn.Trim();
            var nameJa = string.IsNullOrWhiteSpace(model.NameJa) ? nameVi : model.NameJa.Trim();

            void Upsert(string lang, string name, string? meaning)
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

            Upsert("vi", nameVi, model.MeaningVi);
            Upsert("en", nameEn, model.MeaningEn);
            Upsert("ja", nameJa, model.MeaningJa);

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveColor Done Id={Id}", entity.Id);
            return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveColor Error Id={Id}", model.Id);
            throw;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
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
            return AdminListRedirect.ToRefererOrIndex(this);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteColor Error Id={Id}", id);
            throw;
        }
    }
}
