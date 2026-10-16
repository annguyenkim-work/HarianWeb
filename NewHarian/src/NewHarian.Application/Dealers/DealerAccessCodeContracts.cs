namespace NewHarian.Application.Dealers;

public interface IDealerAccessCodeHasher
{
    /// <summary>Keyed hash (hex) of a code already passed through <see cref="DealerAccessCode.Normalize"/>.</summary>
    string Hash(string normalizedCode);
}

public static class DealerCodeMessages
{
    public const string EmailNotQueued = "Chưa gửi được email mã đại lý. Hãy bấm Cấp lại mã.";
    public const string Invalid = "Mã đại lý không hợp lệ.";
    public const string Expired = "Mã đại lý đã hết hiệu lực. Vui lòng nhập lại mã.";
}

/// <param name="EmailQueued">False when the dealer must be sent a new code (email could not be queued).</param>
public sealed record DealerCodeIssueResult(bool Ok, string? Error, bool EmailQueued);

/// <summary>What a checkout session remembers after a valid code: never the code itself.</summary>
public sealed record DealerCheckoutIdentity(int DealerId, DateTime IssuedAt);

public interface IDealerAccessCodeService
{
    /// <summary>Generates a new code (old one stops working), stores its hash and emails the plain code to the dealer.</summary>
    Task<DealerCodeIssueResult> IssueAsync(int dealerId, string? userId, CancellationToken ct = default);

    /// <summary>Null for any failure (malformed / unknown / dealer not approved), so callers cannot tell them apart.</summary>
    Task<DealerCheckoutIdentity?> VerifyAsync(string? code, string? clientIp, CancellationToken ct = default);
}

/// <param name="Expired">The session held a dealer whose code was re-issued or who is no longer approved.</param>
public sealed record DealerCheckoutState(DealerOptionDto? Dealer, bool Expired)
{
    public static DealerCheckoutState None { get; } = new(null, false);
}

/// <summary>Dealer verified for the current guest checkout session.</summary>
public interface IDealerCheckoutContext
{
    /// <summary>Re-checks the dealer on every call; an outdated session is cleared and reported as <c>Expired</c>.</summary>
    Task<DealerCheckoutState> GetAsync(CancellationToken ct = default);
    void Set(DealerCheckoutIdentity identity);
    void Clear();
}
