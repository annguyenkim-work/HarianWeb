using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

/// <summary>Session holds only the dealer id + code issue stamp; every read re-checks both against the database.</summary>
public sealed class SessionDealerCheckoutContext(IHttpContextAccessor http, AppDbContext db) : IDealerCheckoutContext
{
    private const string SessionKey = "DealerCheckout";

    public async Task<DealerCheckoutState> GetAsync(CancellationToken ct = default)
    {
        var stored = Read();
        if (stored is null) return DealerCheckoutState.None;

        var dealer = await db.Dealers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == stored.DealerId, ct);
        if (dealer is { Status: DealerStatus.Approved, AccessCodeIssuedAt: DateTime issuedAt }
            && issuedAt.Ticks == stored.IssuedAtTicks)
            return new DealerCheckoutState(DealerLookup.ToOption(dealer), false);

        Clear();
        return new DealerCheckoutState(null, true);
    }

    public void Set(DealerCheckoutIdentity identity)
        => http.HttpContext?.Session.SetString(SessionKey,
            JsonSerializer.Serialize(new Stored(identity.DealerId, identity.IssuedAt.Ticks)));

    public void Clear() => http.HttpContext?.Session.Remove(SessionKey);

    private Stored? Read()
    {
        var json = http.HttpContext?.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<Stored>(json); }
        catch (JsonException) { return null; }
    }

    private sealed record Stored(int DealerId, long IssuedAtTicks);
}
