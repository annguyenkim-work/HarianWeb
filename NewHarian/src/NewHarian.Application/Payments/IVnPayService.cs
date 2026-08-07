namespace NewHarian.Application.Payments;

public interface IVnPayService
{
    bool IsEnabled { get; }
    /// <summary>Build payment redirect URL for an order. Returns null if disabled.</summary>
    string? CreatePaymentUrl(string orderNumber, decimal amount, string orderInfo, string returnUrl, string ipAddress);
    bool ValidateReturn(IReadOnlyDictionary<string, string> query);
}

public sealed class VnPayOptions
{
    public const string Section = "Payment:VnPay";
    public bool Enabled { get; set; }
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
}
