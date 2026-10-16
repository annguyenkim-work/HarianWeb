using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Address;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Media;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

internal static class DealerProfileMapper
{
    public const string MediaFolder = "dealers";

    /// <summary>Copies a normalized + validated profile onto the entity (resolving address names).</summary>
    public static void Apply(Dealer d, DealerProfileInput m, IVietnamDivisionCatalog catalog)
    {
        catalog.TryResolve(m.ProvinceCode, m.CommuneCode, out var head);
        d.BusinessType = m.BusinessType;
        d.BusinessName = m.BusinessName;
        d.TaxCode = m.TaxCode;
        d.HouseholdRegistrationNo = m.HouseholdRegistrationNo;
        d.RepresentativeName = m.RepresentativeName;
        d.RepresentativeTitle = m.RepresentativeTitle;
        d.CitizenId = m.CitizenId;
        d.FullName = m.FullName;
        d.Phone = m.Phone;
        d.Email = m.Email;
        d.Address = m.Address;
        d.ProvinceCode = head.ProvinceCode;
        d.ProvinceName = head.ProvinceName;
        d.CommuneCode = head.CommuneCode;
        d.CommuneName = head.CommuneName;
        d.ShipToHeadOffice = m.ShipToHeadOffice;
        if (m.ShipToHeadOffice)
        {
            d.ShippingAddress = null;
            d.ShippingProvinceCode = d.ShippingProvinceName = d.ShippingCommuneCode = d.ShippingCommuneName = null;
        }
        else
        {
            catalog.TryResolve(m.ShippingProvinceCode, m.ShippingCommuneCode, out var ship);
            d.ShippingAddress = m.ShippingAddress;
            d.ShippingProvinceCode = ship.ProvinceCode;
            d.ShippingProvinceName = ship.ProvinceName;
            d.ShippingCommuneCode = ship.CommuneCode;
            d.ShippingCommuneName = ship.CommuneName;
        }
        d.InvoiceEmail = m.InvoiceEmail;
        d.Message = m.Message;
    }

    /// <summary>Id of another non-rejected dealer already using the tax code, if any.</summary>
    public static Task<int?> FindTaxCodeOwnerAsync(AppDbContext db, string taxCode, int? excludeId, CancellationToken ct)
        => db.Dealers.AsNoTracking()
            .Where(x => x.TaxCode == taxCode && x.Status != DealerStatus.Rejected && (excludeId == null || x.Id != excludeId))
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The posted media id must be a private upload from the dealers folder not linked to another dealer,
    /// so a crafted form cannot attach someone else's file (e.g. a CV).
    /// </summary>
    public static async Task<bool> IsUsableLicenseAsync(AppDbContext db, int mediaFileId, int? dealerId, CancellationToken ct)
    {
        var prefix = MediaPathResolver.ToPrivateStoredPath(MediaFolder, "");
        var ok = await db.MediaFiles.AsNoTracking()
            .AnyAsync(m => m.Id == mediaFileId && m.IsPrivate && m.StoredPath.StartsWith(prefix), ct);
        if (!ok) return false;
        return !await db.Dealers.AsNoTracking()
            .AnyAsync(x => x.BusinessLicenseMediaFileId == mediaFileId && (dealerId == null || x.Id != dealerId), ct);
    }

    public static string BusinessTypeLabel(DealerBusinessType type)
        => type == DealerBusinessType.Household ? "Hộ kinh doanh" : "Doanh nghiệp";
}
