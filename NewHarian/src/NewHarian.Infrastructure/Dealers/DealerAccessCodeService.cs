using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Dealers;
using NewHarian.Application.Email;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dealers;

/// <summary>Issues / verifies dealer access codes. The plain code exists only in memory and in the (sensitive) email.</summary>
public sealed class DealerAccessCodeService(
    AppDbContext db,
    IDealerAccessCodeHasher hasher,
    ISensitiveEmailSender email,
    IEmailTemplateService emailTemplates,
    IAuditService audit,
    IHttpContextAccessor http,
    ILogger<DealerAccessCodeService> logger) : IDealerAccessCodeService
{
    private const int MaxGenerateAttempts = 5;

    public async Task<DealerCodeIssueResult> IssueAsync(int dealerId, string? userId, CancellationToken ct = default)
    {
        logger.LogInformation("IssueDealerCode Start DealerId={DealerId}", dealerId);
        try
        {
            var d = await db.Dealers.FirstOrDefaultAsync(x => x.Id == dealerId, ct);
            var err = d is null ? "Không tìm thấy đại lý."
                : d.Status != DealerStatus.Approved ? "Chỉ cấp mã cho đại lý đã duyệt."
                : null;
            if (err is not null)
            {
                logger.LogWarning("IssueDealerCode Done rejected DealerId={DealerId} Error={Error}", dealerId, err);
                return new DealerCodeIssueResult(false, err, false);
            }

            var (code, hash) = await NewUniqueCodeAsync(dealerId, ct);
            var oldLast4 = d!.AccessCodeLast4;
            d.AccessCodeHash = hash;
            d.AccessCodeLast4 = DealerAccessCode.Last4(code);
            d.AccessCodeIssuedAt = DateTime.UtcNow;
            d.AccessCodeIssuedByUserId = userId;
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync("Dealer.CodeIssued", "Dealer", d.Id.ToString(),
                oldLast4 is null ? null : new { Last4 = oldLast4 },
                new { Last4 = d.AccessCodeLast4, By = userId }, ct);

            var queued = await SendCodeAsync(d.Email, d.BusinessName, code, d.Id, ct);
            logger.LogInformation("IssueDealerCode Done DealerId={DealerId} Last4={Last4} EmailQueued={Queued}",
                dealerId, d.AccessCodeLast4, queued);
            return new DealerCodeIssueResult(true, null, queued);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IssueDealerCode Error DealerId={DealerId}", dealerId);
            throw;
        }
    }

    public async Task<DealerCheckoutIdentity?> VerifyAsync(string? code, string? clientIp, CancellationToken ct = default)
    {
        logger.LogInformation("VerifyDealerCode Start Ip={Ip}", clientIp);
        var normalized = DealerAccessCode.Normalize(code);
        if (normalized is not null)
        {
            var hash = hasher.Hash(normalized);
            var match = await db.Dealers.AsNoTracking()
                .Where(x => x.AccessCodeHash == hash && x.Status == DealerStatus.Approved && x.AccessCodeIssuedAt != null)
                .Select(x => new { x.Id, IssuedAt = x.AccessCodeIssuedAt!.Value })
                .FirstOrDefaultAsync(ct);
            if (match is not null)
            {
                logger.LogInformation("VerifyDealerCode Done DealerId={DealerId} Ip={Ip}", match.Id, clientIp);
                return new DealerCheckoutIdentity(match.Id, match.IssuedAt);
            }
        }

        logger.LogWarning("VerifyDealerCode Done rejected Ip={Ip}", clientIp);
        return null;
    }

    private async Task<(string Code, string Hash)> NewUniqueCodeAsync(int dealerId, CancellationToken ct)
    {
        for (var i = 0; i < MaxGenerateAttempts; i++)
        {
            var code = DealerAccessCode.Generate();
            var hash = hasher.Hash(code);
            if (!await db.Dealers.AnyAsync(x => x.AccessCodeHash == hash && x.Id != dealerId, ct))
                return (code, hash);
        }
        throw new InvalidOperationException("Could not generate a unique dealer access code.");
    }

    private async Task<bool> SendCodeAsync(string to, string businessName, string code, int dealerId, CancellationToken ct)
    {
        try
        {
            var request = http.HttpContext?.Request;
            var shopUrl = request is null ? "/cart" : $"{request.Scheme}://{request.Host}/cart";
            var (subject, body) = await emailTemplates.RenderAsync(EmailTemplateCodes.DealerApproved, new Dictionary<string, string?>
            {
                ["BusinessName"] = EmailTemplateService.Enc(businessName),
                ["AccessCode"] = EmailTemplateService.Enc(DealerAccessCode.Format(code)),
                ["ShopUrl"] = EmailTemplateService.Enc(shopUrl)
            }, ct);
            await email.SendSensitiveAsync(to, subject, body, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IssueDealerCode email failed DealerId={DealerId}", dealerId);
            return false;
        }
    }
}
