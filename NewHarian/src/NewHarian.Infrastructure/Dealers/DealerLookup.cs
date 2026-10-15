using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Address;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

public sealed class DealerLookup(AppDbContext db) : IDealerLookup
{
    public async Task<IReadOnlyList<DealerOptionDto>> ListApprovedOptionsAsync(CancellationToken ct = default)
    {
        var rows = await db.Dealers.AsNoTracking()
            .Where(d => d.Status == DealerStatus.Approved)
            .OrderBy(d => d.BusinessName)
            .ToListAsync(ct);

        return rows.Select(ToOption).ToList();
    }

    internal static DealerOptionDto ToOption(Dealer d) => new()
    {
        Id = d.Id,
        BusinessType = d.BusinessType,
        BusinessName = d.BusinessName,
        TaxCode = d.TaxCode,
        FullName = d.FullName,
        Phone = d.Phone,
        Email = d.Email,
        CitizenId = d.CitizenId,
        ShipAddress = d.ShipToHeadOffice ? d.Address : d.ShippingAddress ?? "",
        ShipProvinceCode = d.ShipToHeadOffice ? d.ProvinceCode : d.ShippingProvinceCode,
        ShipCommuneCode = d.ShipToHeadOffice ? d.CommuneCode : d.ShippingCommuneCode,
        InvoiceAddress = AddressFormat.Join(d.Address, d.CommuneName, d.ProvinceName),
        InvoiceEmail = d.InvoiceEmail ?? d.Email
    };
}
