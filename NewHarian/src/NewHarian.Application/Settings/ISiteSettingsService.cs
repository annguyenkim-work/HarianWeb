namespace NewHarian.Application.Settings;

public sealed class BankSettingsDto
{
    public string BankBin { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccount { get; set; } = "";
    public string AccountHolderName { get; set; } = "";
    public string BankBranch { get; set; } = "";
    public string? VietQrPreviewDataUrl { get; set; }
}

public sealed class BrandSettingsDto
{
    public string BrandName { get; set; } = "Harian";
    public string CompanyName { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Phone2 { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string TaglineVi { get; set; } = "";
    public string TaglineEn { get; set; } = "";
    public string TaglineJa { get; set; } = "";
    public string FacebookUrl { get; set; } = "";
    public string InstagramUrl { get; set; } = "";
}

public sealed class EmailSettingsDto
{
    public string OrderEmail { get; set; } = "";
    public string InquiryEmail { get; set; } = "";
    public string ApplicationEmail { get; set; } = "";
    public string ServiceBookingEmail { get; set; } = "";
    public string DealerEmail { get; set; } = "";
    public string CompanyEmail { get; set; } = "";
    public string TestTo { get; set; } = "";
    public bool SmtpEnabled { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpFrom { get; set; } = "";
    public string SmtpFromName { get; set; } = "Harian";
    public bool SmtpUseSsl { get; set; } = true;
}

public sealed class InventorySettingsDto
{
    public int LowStockThreshold { get; set; } = 5;
    /// <summary>Lots with HSD within this many days count as expiring soon. Default 30.</summary>
    public int ExpiringWithinDays { get; set; } = 30;
}

public sealed record SettingsFileUpload(
    Stream Content,
    string FileName,
    string ContentType,
    string? UploadedByUserId);

public sealed record SettingsSaveResult(bool Ok, IReadOnlyDictionary<string, string>? Errors = null);
public sealed record TestEmailResult(bool Ok, string Message);

public interface ISiteSettingsService
{
    Task<BankSettingsDto> GetBankAsync(CancellationToken ct = default);
    Task SaveBankAsync(BankSettingsDto model, CancellationToken ct = default);
    Task<BrandSettingsDto> GetBrandAsync(CancellationToken ct = default);
    Task SaveBrandAsync(BrandSettingsDto model, SettingsFileUpload? logo, CancellationToken ct = default);
    Task ClearLogoAsync(CancellationToken ct = default);
    Task<EmailSettingsDto> GetEmailAsync(CancellationToken ct = default);
    Task<SettingsSaveResult> SaveEmailAsync(EmailSettingsDto model, CancellationToken ct = default);
    Task<TestEmailResult> TestEmailAsync(string? testTo, CancellationToken ct = default);
    Task<InventorySettingsDto> GetInventoryAsync(CancellationToken ct = default);
    Task<SettingsSaveResult> SaveInventoryAsync(InventorySettingsDto model, CancellationToken ct = default);
}
