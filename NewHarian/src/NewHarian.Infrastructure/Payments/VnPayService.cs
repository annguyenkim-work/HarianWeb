using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NewHarian.Application.Payments;

namespace NewHarian.Infrastructure.Payments;

public sealed class VnPayService(IConfiguration config) : IVnPayService
{
    public bool IsEnabled => config.GetValue("Payment:VnPay:Enabled", false)
        && !string.IsNullOrWhiteSpace(config["Payment:VnPay:TmnCode"])
        && !string.IsNullOrWhiteSpace(config["Payment:VnPay:HashSecret"]);

    public string? CreatePaymentUrl(string orderNumber, decimal amount, string orderInfo, string returnUrl, string ipAddress)
    {
        if (!IsEnabled) return null;

        var tmn = config["Payment:VnPay:TmnCode"]!;
        var secret = config["Payment:VnPay:HashSecret"]!;
        var baseUrl = config["Payment:VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

        var tick = DateTime.Now.ToString("yyyyMMddHHmmss");
        var amountVnd = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture);

        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmn,
            ["vnp_Amount"] = amountVnd,
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = orderNumber,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_CreateDate"] = tick
        };

        var query = string.Join("&", data.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));
        var sign = HmacSha512(secret, query);
        return $"{baseUrl}?{query}&vnp_SecureHash={sign}";
    }

    public bool ValidateReturn(IReadOnlyDictionary<string, string> query)
    {
        if (!IsEnabled) return false;
        if (!query.TryGetValue("vnp_SecureHash", out var hash) || string.IsNullOrEmpty(hash))
            return false;

        var secret = config["Payment:VnPay:HashSecret"]!;
        var data = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in query)
        {
            if (k.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                && k is not ("vnp_SecureHash" or "vnp_SecureHashType"))
                data[k] = v;
        }
        var raw = string.Join("&", data.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));
        var expected = HmacSha512(secret, raw);
        return string.Equals(expected, hash, StringComparison.OrdinalIgnoreCase)
               && query.TryGetValue("vnp_ResponseCode", out var code) && code == "00";
    }

    private static string HmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var input = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(input)).ToLowerInvariant();
    }
}
