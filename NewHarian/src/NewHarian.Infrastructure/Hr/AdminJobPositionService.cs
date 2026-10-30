using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Hr;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Hr;

public sealed class AdminJobPositionService(AppDbContext db, ILogger<AdminJobPositionService> logger) : IAdminJobPositionService
{
    public async Task<IReadOnlyList<JobPositionListItemDto>> ListAsync(CancellationToken ct = default)
        => await db.JobPositions.AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .Select(p => new JobPositionListItemDto(p.Id, p.Name, p.SortOrder, p.IsActive))
            .ToListAsync(ct);

    public async Task<JobPositionForm?> GetAsync(int id, CancellationToken ct = default)
    {
        var row = await db.JobPositions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return row is null ? null : new JobPositionForm { Id = row.Id, Name = row.Name, IsActive = row.IsActive };
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveAsync(JobPositionForm form, CancellationToken ct = default)
    {
        logger.LogInformation("SaveJobPosition Start Id={Id} Name={Name}", form.Id, form.Name);
        try
        {
            if (JobPositionPolicy.ValidateName(form.Name) is { } nameError)
                return Reject(form, nameError);

            var name = form.Name.Trim();
            var existing = await db.JobPositions.AsNoTracking().Select(p => new { p.Id, p.Name }).ToListAsync(ct);
            if (JobPositionPolicy.IsDuplicate(name, existing.Select(x => (x.Id, x.Name)), form.Id))
                return Reject(form, JobPositionPolicy.NameDuplicate);

            JobPosition row;
            if (form.Id is int id)
            {
                var found = await db.JobPositions.FirstOrDefaultAsync(p => p.Id == id, ct);
                if (found is null) return Reject(form, JobPositionPolicy.Missing);
                row = found;
                row.Name = name;
                row.IsActive = form.IsActive;
                row.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var max = await db.JobPositions.MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;
                row = new JobPosition
                {
                    Name = name,
                    IsActive = form.IsActive,
                    SortOrder = max + 1,
                    CreatedAt = DateTime.UtcNow
                };
                db.JobPositions.Add(row);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveJobPosition Done Id={Id} Name={Name} IsActive={IsActive}", row.Id, row.Name, row.IsActive);
            return (true, null, row.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveJobPosition Error Id={Id}", form.Id);
            throw;
        }
    }

    public async Task MoveAsync(int id, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("MoveJobPosition Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var items = await db.JobPositions.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
            var idx = items.FindIndex(p => p.Id == id);
            var swapIdx = idx + direction;
            if (idx < 0 || swapIdx < 0 || swapIdx >= items.Count)
            {
                logger.LogInformation("MoveJobPosition Done Id={Id} skipped", id);
                return;
            }

            (items[idx].SortOrder, items[swapIdx].SortOrder) = (items[swapIdx].SortOrder, items[idx].SortOrder);
            items[idx].UpdatedAt = DateTime.UtcNow;
            items[swapIdx].UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("MoveJobPosition Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveJobPosition Error Id={Id}", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<JobPositionOptionDto>> ListChoicesAsync(int? includeId, CancellationToken ct = default)
    {
        var all = await db.JobPositions.AsNoTracking()
            .Select(p => new JobPositionChoice(p.Id, p.Name, p.IsActive, p.SortOrder))
            .ToListAsync(ct);
        return JobPositionPolicy.Choices(all, includeId)
            .Select(p => new JobPositionOptionDto(p.Id, p.Name, p.IsActive))
            .ToList();
    }

    private (bool Ok, string? Error, int? Id) Reject(JobPositionForm form, string error)
    {
        logger.LogWarning("SaveJobPosition Done rejected Id={Id} Error={Error}", form.Id, error);
        return (false, error, null);
    }
}
