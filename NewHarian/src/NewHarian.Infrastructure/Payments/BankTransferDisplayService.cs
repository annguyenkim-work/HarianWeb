using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Payments;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Payments;

public sealed class BankTransferDisplayService(AppDbContext db, IVietQrService vietQr) : IBankTransferDisplayService
{
    public async Task<BankTransferDisplay> BuildAsync(decimal amount, string orderNumber, CancellationToken ct = default)
    {
        var keys = new[]
        {
            "company.bank.name",
            "company.bank.account",
            "company.bank.branch",
            "company.bank.bin",
            "company.bank.account_name"
        };
        var map = await db.SiteSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        map.TryGetValue("company.bank.name", out var bankName);
        map.TryGetValue("company.bank.account", out var account);
        map.TryGetValue("company.bank.branch", out var branch);
        map.TryGetValue("company.bank.bin", out var bin);
        map.TryGetValue("company.bank.account_name", out var holder);

        var amountVnd = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        string? qr = null;
        if (!string.IsNullOrWhiteSpace(bin) && !string.IsNullOrWhiteSpace(account))
            qr = vietQr.CreatePngDataUrl(bin!, account!, amountVnd, orderNumber, holder);

        return new BankTransferDisplay(
            BankName: bankName,
            BankAccount: account,
            BankBranch: branch,
            AccountHolderName: holder,
            QrSrc: qr,
            AmountText: $"{amount:N0}đ",
            TransferContent: orderNumber);
    }
}
