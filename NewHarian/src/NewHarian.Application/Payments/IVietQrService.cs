namespace NewHarian.Application.Payments;

public interface IVietQrService
{
    /// <summary>EMVCo NAPAS VietQR payload, or null if inputs are invalid.</summary>
    string? BuildPayload(string bankBin, string accountNumber, long amountVnd, string purpose, string? accountName = null);

    /// <summary>PNG as data:image/png;base64,... or null.</summary>
    string? CreatePngDataUrl(string bankBin, string accountNumber, long amountVnd, string purpose, string? accountName = null);
}

public interface IBankTransferDisplayService
{
    Task<BankTransferDisplay> BuildAsync(decimal amount, string orderNumber, CancellationToken ct = default);
}

public sealed record BankTransferDisplay(
    string? BankName,
    string? BankAccount,
    string? BankBranch,
    string? AccountHolderName,
    /// <summary>Dynamic VietQR data-URL, or null if bank settings incomplete.</summary>
    string? QrSrc,
    string AmountText,
    string TransferContent);

/// <summary>Common NAPAS BINs for Admin bank picker.</summary>
public static class VnBankCatalog
{
    public sealed record Entry(string Bin, string ShortName, string DisplayName);

    public static IReadOnlyList<Entry> All { get; } =
    [
        new("970436", "VCB", "Vietcombank"),
        new("970415", "CTG", "VietinBank"),
        new("970418", "BIDV", "BIDV"),
        new("970405", "VBA", "Agribank"),
        new("970422", "MB", "MB Bank"),
        new("970407", "TCB", "Techcombank"),
        new("970432", "VPB", "VPBank"),
        new("970423", "TPB", "TPBank"),
        new("970403", "STB", "Sacombank"),
        new("970416", "ACB", "ACB"),
        new("970441", "VIB", "VIB"),
        new("970448", "OCB", "OCB"),
        new("970437", "HDB", "HDBank"),
        new("970426", "MSB", "MSB"),
        new("970431", "EIB", "Eximbank"),
        new("970443", "SHB", "SHB"),
        new("970440", "SEAB", "SeABank"),
        new("970449", "LPB", "LPBank"),
        new("970412", "PVCB", "PVcomBank"),
        new("970414", "DOB", "DongA Bank"),
        new("970409", "BAB", "Bac A Bank"),
        new("970425", "ABB", "ABBank"),
        new("970427", "VAB", "VietABank"),
        new("970428", "NAB", "Nam A Bank"),
        new("970438", "BVB", "BaoViet Bank"),
        new("970452", "KLB", "KienlongBank"),
        new("970424", "SHBVN", "Shinhan Bank"),
        new("970457", "WVN", "Woori Bank"),
        new("970458", "UOB", "UOB"),
        new("970454", "VCCB", "BVBank")
    ];

    public static Entry? FindByBin(string? bin)
        => string.IsNullOrWhiteSpace(bin) ? null : All.FirstOrDefault(e => e.Bin == bin.Trim());
}
