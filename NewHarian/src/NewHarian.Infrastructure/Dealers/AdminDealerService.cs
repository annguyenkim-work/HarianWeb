using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Address;
using NewHarian.Application.Admin;
using NewHarian.Application.Dealers;
using NewHarian.Application.Validation;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

public sealed class AdminDealerService(
    AppDbContext db,
    IAuditService audit,
    IVietnamDivisionCatalog catalog,
    IDealerAccessCodeService accessCodes,
    ILogger<AdminDealerService> logger) : IAdminDealerService
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "name", "taxCode", "phone", "status"
    };

    public async Task<IReadOnlyList<DealerListItemDto>> ListAsync(
        DealerStatus? status, DealerBusinessType? type = null, string? q = null, string? sort = null, string? dir = null,
        CancellationToken ct = default)
    {
        var query = db.Dealers.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(d => d.Status == status);
        if (type is not null)
            query = query.Where(d => d.BusinessType == type);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(d =>
                d.BusinessName.ToLower().Contains(term) ||
                d.TaxCode.Contains(term) ||
                d.FullName.ToLower().Contains(term) ||
                d.Email.ToLower().Contains(term) ||
                d.Phone.Contains(term));
        }

        var sortKey = AdminListQuery.NormalizeSort(sort, SortKeys, "createdAt");
        var sortDir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sortKey));
        var asc = AdminListQuery.IsAsc(sortDir);
        query = (sortKey, asc) switch
        {
            ("id", true) => query.OrderBy(d => d.Id),
            ("id", false) => query.OrderByDescending(d => d.Id),
            ("name", true) => query.OrderBy(d => d.BusinessName).ThenByDescending(d => d.Id),
            ("name", false) => query.OrderByDescending(d => d.BusinessName).ThenByDescending(d => d.Id),
            ("taxCode", true) => query.OrderBy(d => d.TaxCode).ThenByDescending(d => d.Id),
            ("taxCode", false) => query.OrderByDescending(d => d.TaxCode).ThenByDescending(d => d.Id),
            ("phone", true) => query.OrderBy(d => d.Phone).ThenByDescending(d => d.Id),
            ("phone", false) => query.OrderByDescending(d => d.Phone).ThenByDescending(d => d.Id),
            ("status", true) => query.OrderBy(d => d.Status).ThenByDescending(d => d.Id),
            ("status", false) => query.OrderByDescending(d => d.Status).ThenByDescending(d => d.Id),
            ("createdAt", true) => query.OrderBy(d => d.CreatedAt).ThenByDescending(d => d.Id),
            _ => query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id),
        };

        return await query
            .Select(d => new DealerListItemDto(
                d.Id, d.CreatedAt, d.BusinessType, d.BusinessName, d.TaxCode, d.FullName, d.Phone, d.Email, d.Status,
                d.DiscountOverrides.Any()))
            .ToListAsync(ct);
    }

    public async Task<DealerDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Dealers.AsNoTracking().Include(x => x.BusinessLicense).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        return new DealerDetailDto
        {
            Id = d.Id,
            CreatedAt = d.CreatedAt,
            BusinessType = d.BusinessType,
            BusinessName = d.BusinessName,
            TaxCode = d.TaxCode,
            HouseholdRegistrationNo = d.HouseholdRegistrationNo,
            RepresentativeName = d.RepresentativeName,
            RepresentativeTitle = d.RepresentativeTitle,
            CitizenId = d.CitizenId,
            FullName = d.FullName,
            Phone = d.Phone,
            Email = d.Email,
            HeadOfficeAddress = AddressFormat.Join(d.Address, d.CommuneName, d.ProvinceName),
            ShipToHeadOffice = d.ShipToHeadOffice,
            ShippingAddress = d.ShipToHeadOffice
                ? null
                : AddressFormat.Join(d.ShippingAddress, d.ShippingCommuneName, d.ShippingProvinceName),
            InvoiceEmail = d.InvoiceEmail,
            BusinessLicenseMediaFileId = d.BusinessLicenseMediaFileId,
            BusinessLicenseFileName = d.BusinessLicense?.FileName,
            Message = d.Message,
            Status = d.Status,
            InternalNotes = d.InternalNotes,
            ReviewedByUserId = d.ReviewedByUserId,
            LanguageCode = d.LanguageCode,
            ReviewedAt = d.ReviewedAt,
            AccessCodeLast4 = d.AccessCodeLast4,
            AccessCodeIssuedAt = d.AccessCodeIssuedAt,
            AccessCodeIssuedByUserId = d.AccessCodeIssuedByUserId
        };
    }

    public async Task<DealerEditRequest?> GetEditAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Dealers.AsNoTracking().Include(x => x.BusinessLicense).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        return new DealerEditRequest
        {
            Id = d.Id,
            BusinessType = d.BusinessType,
            BusinessName = d.BusinessName,
            TaxCode = d.TaxCode,
            HouseholdRegistrationNo = d.HouseholdRegistrationNo,
            RepresentativeName = d.RepresentativeName,
            RepresentativeTitle = d.RepresentativeTitle,
            CitizenId = d.CitizenId,
            FullName = d.FullName,
            Phone = d.Phone,
            Email = d.Email,
            ProvinceCode = d.ProvinceCode ?? "",
            CommuneCode = d.CommuneCode ?? "",
            Address = d.Address,
            ShipToHeadOffice = d.ShipToHeadOffice,
            ShippingProvinceCode = d.ShippingProvinceCode,
            ShippingCommuneCode = d.ShippingCommuneCode,
            ShippingAddress = d.ShippingAddress,
            InvoiceEmail = d.InvoiceEmail,
            Message = d.Message,
            InternalNotes = d.InternalNotes,
            BusinessLicenseMediaFileId = d.BusinessLicenseMediaFileId,
            BusinessLicenseFileName = d.BusinessLicense?.FileName
        };
    }

    public async Task<(bool Ok, string? Error, int? Id, string? Warning)> SaveAsync(DealerEditRequest request, string? userId, CancellationToken ct = default)
    {
        var action = request.Id is null ? "CreateDealer" : "UpdateDealer";
        logger.LogInformation("{Action} Start Id={Id}", action, request.Id);
        try
        {
            DealerProfileValidator.Normalize(request);
            var err = DealerProfileValidator.Validate(request, catalog);
            if (err is null && !FitsNotes(request.InternalNotes))
                err = "Ghi chú nội bộ tối đa 2000 ký tự.";

            Dealer? d = null;
            if (err is null && request.Id is int id)
            {
                d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (d is null) err = "Không tìm thấy đại lý.";
            }

            var checkDuplicate = d is null || d.Status != DealerStatus.Rejected;
            if (err is null && checkDuplicate
                && await DealerProfileMapper.FindTaxCodeOwnerAsync(db, request.TaxCode, d?.Id, ct) is int owner)
                err = $"MST này đã thuộc đại lý #{owner}.";

            int? newLicense = null;
            if (err is null && !request.RemoveBusinessLicense && request.BusinessLicenseMediaFileId is int mediaId
                && mediaId != d?.BusinessLicenseMediaFileId)
            {
                if (await DealerProfileMapper.IsUsableLicenseAsync(db, mediaId, d?.Id, ct))
                    newLicense = mediaId;
                else
                    err = "File giấy đăng ký kinh doanh không hợp lệ.";
            }

            if (err is not null)
            {
                logger.LogWarning("{Action} Done rejected Id={Id} Error={Error}", action, request.Id, err);
                return (false, err, null, null);
            }

            var notes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim();
            string? warning = null;
            if (d is null)
            {
                var now = DateTime.UtcNow;
                d = new Dealer
                {
                    Status = DealerStatus.Approved,
                    ReviewedByUserId = userId,
                    ReviewedAt = now,
                    LanguageCode = "vi",
                    CreatedAt = now,
                    InternalNotes = notes,
                    BusinessLicenseMediaFileId = newLicense
                };
                DealerProfileMapper.Apply(d, request, catalog);
                db.Dealers.Add(d);
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync("Dealer.Created", "Dealer", d.Id.ToString(), null,
                    new { d.BusinessType, d.BusinessName, d.TaxCode, d.Email }, ct);
                warning = await IssueCodeAsync(d.Id, userId, ct);
            }
            else
            {
                var before = new { d.BusinessName, d.TaxCode };
                DealerProfileMapper.Apply(d, request, catalog);
                d.InternalNotes = notes;
                if (request.RemoveBusinessLicense)
                    d.BusinessLicenseMediaFileId = null;
                else if (newLicense is not null)
                    d.BusinessLicenseMediaFileId = newLicense;
                await db.SaveChangesAsync(ct);
                if (before.BusinessName != d.BusinessName || before.TaxCode != d.TaxCode)
                    await audit.WriteAsync("Dealer.Updated", "Dealer", d.Id.ToString(), before,
                        new { d.BusinessName, d.TaxCode }, ct);
            }

            logger.LogInformation("{Action} Done Id={Id}", action, d.Id);
            return (true, null, d.Id, warning);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Action} Error Id={Id}", action, request.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error, string? Warning)> ApproveAsync(int id, string? notes, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("ApproveDealer Start Id={Id}", id);
        try
        {
            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
            string? err = null;
            if (d is null)
                err = "Không tìm thấy.";
            else if (d.Status == DealerStatus.Approved)
                err = "Đại lý đã được duyệt.";
            else if (await DealerProfileMapper.FindTaxCodeOwnerAsync(db, d.TaxCode, d.Id, ct) is int owner)
                err = $"MST này đã thuộc đại lý #{owner}.";
            if (err is not null)
            {
                logger.LogWarning("ApproveDealer Done rejected Id={Id} Error={Error}", id, err);
                return (false, err, null);
            }

            var from = d!.Status;
            d.Status = DealerStatus.Approved;
            if (notes is not null) d.InternalNotes = notes.Trim();
            d.ReviewedAt = DateTime.UtcNow;
            d.ReviewedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.Approved", "Dealer", d.Id.ToString(),
                new { Status = from.ToString() }, new { Status = d.Status.ToString() }, ct);
            var warning = await IssueCodeAsync(d.Id, userId, ct);
            logger.LogInformation("ApproveDealer Done Id={Id}", id);
            return (true, null, warning);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ApproveDealer Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> RejectAsync(int id, string? notes, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("RejectDealer Start Id={Id}", id);
        try
        {
            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null)
            {
                logger.LogWarning("RejectDealer Done rejected Id={Id}", id);
                return (false, "Không tìm thấy.");
            }
            if (d.Status == DealerStatus.Approved)
            {
                logger.LogWarning("RejectDealer Done rejected Id={Id} Error={Error}", id, "already approved");
                return (false, "Không từ chối đại lý đã duyệt.");
            }

            var from = d.Status;
            d.Status = DealerStatus.Rejected;
            if (notes is not null) d.InternalNotes = notes.Trim();
            d.ReviewedAt = DateTime.UtcNow;
            d.ReviewedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Dealer.Rejected", "Dealer", d.Id.ToString(),
                new { Status = from.ToString() }, new { Status = d.Status.ToString() }, ct);
            logger.LogInformation("RejectDealer Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RejectDealer Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> SaveNotesAsync(int id, string? notes, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveDealer Start Id={Id}", id);
        try
        {
            if (!FitsNotes(notes))
            {
                logger.LogWarning("SaveDealer Done rejected Id={Id} Error={Error}", id, "notes too long");
                return (false, "Ghi chú nội bộ tối đa 2000 ký tự.");
            }
            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null)
            {
                logger.LogWarning("SaveDealer Done rejected Id={Id}", id);
                return (false, "Không tìm thấy.");
            }

            d.InternalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            d.ReviewedByUserId = userId ?? d.ReviewedByUserId;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveDealer Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveDealer Error Id={Id}", id);
            throw;
        }
    }

    public Task<int?> GetBusinessLicenseMediaIdAsync(int id, CancellationToken ct = default)
        => db.Dealers.AsNoTracking().Where(x => x.Id == id).Select(x => x.BusinessLicenseMediaFileId).FirstOrDefaultAsync(ct);

    private async Task<string?> IssueCodeAsync(int dealerId, string? userId, CancellationToken ct)
    {
        var issued = await accessCodes.IssueAsync(dealerId, userId, ct);
        return issued.Ok && issued.EmailQueued ? null : DealerCodeMessages.EmailNotQueued;
    }

    private static bool FitsNotes(string? notes) => GuestValidation.FitsMax(notes, GuestValidation.NotesMax);
}
