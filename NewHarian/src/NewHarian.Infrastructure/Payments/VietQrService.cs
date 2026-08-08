using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NewHarian.Application.Payments;
using QRCoder;

namespace NewHarian.Infrastructure.Payments;

public sealed class VietQrService : IVietQrService
{
    private const int PurposeMax = 25;
    private const int NameMax = 25;

    public string? BuildPayload(string bankBin, string accountNumber, long amountVnd, string purpose, string? accountName = null)
    {
        var bin = DigitsOnly(bankBin);
        var account = DigitsOnly(accountNumber);
        if (bin.Length != 6 || account.Length is < 6 or > 19)
            return null;
        if (amountVnd < 0)
            return null;

        var note = TruncateAscii(SanitizePurpose(purpose), PurposeMax);
        var merchant = TruncateAscii(ToEmvMerchantName(accountName), NameMax);
        if (string.IsNullOrEmpty(merchant))
            merchant = "KHACH";

        var consumer = Tlv("00", bin) + Tlv("01", account);
        var merchantAccount = Tlv("00", "A000000727") + Tlv("01", consumer) + Tlv("02", "QRIBFTTA");

        var sb = new StringBuilder(160);
        sb.Append(Tlv("00", "01"));
        sb.Append(Tlv("01", amountVnd > 0 ? "12" : "11"));
        sb.Append(Tlv("38", merchantAccount));
        sb.Append(Tlv("53", "704"));
        if (amountVnd > 0)
            sb.Append(Tlv("54", amountVnd.ToString(CultureInfo.InvariantCulture)));
        sb.Append(Tlv("58", "VN"));
        sb.Append(Tlv("59", merchant));
        sb.Append(Tlv("60", "HANOI"));
        if (!string.IsNullOrEmpty(note))
            sb.Append(Tlv("62", Tlv("08", note)));

        sb.Append("6304");
        sb.Append(Crc16CcittFalse(sb.ToString()));
        return sb.ToString();
    }

    public string? CreatePngDataUrl(string bankBin, string accountNumber, long amountVnd, string purpose, string? accountName = null)
    {
        var payload = BuildPayload(bankBin, accountNumber, amountVnd, purpose, accountName);
        if (payload is null)
            return null;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(5);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    private static string Tlv(string id, string value)
        => id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;

    private static string DigitsOnly(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    private static string SanitizePurpose(string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose)) return "";
        // EMV additional data: prefer ASCII; keep alnum + hyphen/underscore/space
        var cleaned = Regex.Replace(purpose.Trim(), @"[^A-Za-z0-9\-_\s]", "");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static string ToEmvMerchantName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (c is 'đ' or 'Đ')
            {
                sb.Append(c == 'đ' ? 'd' : 'D');
                continue;
            }
            if (c <= 0x7F && (char.IsLetterOrDigit(c) || c == ' '))
                sb.Append(c);
        }
        return Regex.Replace(sb.ToString().Trim(), @"\s+", " ");
    }

    private static string TruncateAscii(string value, int max)
        => value.Length <= max ? value : value[..max];

    /// <summary>CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF).</summary>
    private static string Crc16CcittFalse(string data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
