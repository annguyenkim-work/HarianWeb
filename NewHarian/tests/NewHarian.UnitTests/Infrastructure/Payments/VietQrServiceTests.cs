using System.Text;
using NewHarian.Infrastructure.Payments;

namespace NewHarian.UnitTests.Infrastructure.Payments;

[Trait("Category", "Unit")]
public class VietQrServiceTests
{
    private const string Bin = "970436";
    private const string Account = "0123456789";

    /// <summary>Hand-assembled TLVs; CRC computed independently (CRC-16/CCITT-FALSE).</summary>
    private const string DynamicGolden =
        "000201" + "010212"
        + "3854" + "0010A000000727" + "0124" + "0006970436" + "01100123456789" + "0208QRIBFTTA"
        + "5303704" + "5406150000" + "5802VN" + "5914Nguyen Van Duc" + "6005HANOI"
        + "6218" + "0814HAR-ORDER-0001"
        + "6304E244";

    private const string StaticGolden =
        "000201" + "010211"
        + "3854" + "0010A000000727" + "0124" + "0006970436" + "01100123456789" + "0208QRIBFTTA"
        + "5303704" + "5802VN" + "5905KHACH" + "6005HANOI"
        + "630421C3";

    private readonly VietQrService _sut = new();

    [Fact]
    public void Builds_dynamic_payload_matching_golden_string()
    {
        Assert.Equal(DynamicGolden, _sut.BuildPayload(Bin, Account, 150_000, "HAR-ORDER-0001", "Nguyễn Văn Đức"));
    }

    [Fact]
    public void Builds_static_payload_without_amount_note_or_name()
    {
        Assert.Equal(StaticGolden, _sut.BuildPayload(Bin, Account, 0, "", null));
    }

    [Fact]
    public void Strips_non_digits_from_bin_and_account()
    {
        Assert.Equal(DynamicGolden, _sut.BuildPayload("970-436", "0123 456 789", 150_000, "HAR-ORDER-0001", "Nguyễn Văn Đức"));
    }

    [Fact]
    public void Crc_helper_matches_standard_check_value()
    {
        Assert.Equal("29B1", Crc16CcittFalse("123456789"));
    }

    [Theory]
    [InlineData(150_000, "HAR-ORDER-0001", "Harian")]
    [InlineData(1, "x", null)]
    [InlineData(0, "", "Công ty TNHH Thương mại Dịch vụ Harian")]
    [InlineData(99_999_999, "HAR ORDER 0099 thanh toan don hang", "A")]
    public void Crc_trailer_covers_everything_before_it(long amount, string purpose, string? name)
    {
        var payload = _sut.BuildPayload(Bin, Account, amount, purpose, name)!;

        Assert.Equal("6304", payload[^8..^4]);
        Assert.Equal(Crc16CcittFalse(payload[..^4]), payload[^4..]);
    }

    [Theory]
    [InlineData(150_000, "HAR-ORDER-0001", "Nguyễn Văn Đức")]
    [InlineData(0, "", null)]
    [InlineData(5, "HAR ORDER 0099 thanh toan don hang so 1", "Công ty TNHH Thương mại Dịch vụ Harian")]
    public void Payload_is_well_formed_tlv(long amount, string purpose, string? name)
    {
        var payload = _sut.BuildPayload(Bin, Account, amount, purpose, name)!;

        var tags = ParseTlv(payload);

        Assert.Equal(new[] { "00", "01", "38", "53" }, tags.Take(4).Select(t => t.Id));
        Assert.Equal("63", tags[^1].Id);
        Assert.Equal(amount > 0, tags.Any(t => t.Id == "54"));
    }

    [Theory]
    [InlineData("97043")]
    [InlineData("9704361")]
    [InlineData("")]
    [InlineData("abcdef")]
    public void Returns_null_for_invalid_bin(string bin)
    {
        Assert.Null(_sut.BuildPayload(bin, Account, 1000, "x"));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345678901234567890")]
    [InlineData("")]
    public void Returns_null_for_invalid_account(string account)
    {
        Assert.Null(_sut.BuildPayload(Bin, account, 1000, "x"));
    }

    [Fact]
    public void Accepts_account_length_bounds()
    {
        Assert.NotNull(_sut.BuildPayload(Bin, "123456", 1000, "x"));
        Assert.NotNull(_sut.BuildPayload(Bin, "1234567890123456789", 1000, "x"));
    }

    [Fact]
    public void Returns_null_for_negative_amount()
    {
        Assert.Null(_sut.BuildPayload(Bin, Account, -1, "x"));
    }

    [Fact]
    public void Truncates_transfer_content_to_25_chars()
    {
        var payload = _sut.BuildPayload(Bin, Account, 1000, "HAR-ORDER-0001 THANH TOAN DON HANG")!;

        Assert.Contains("6229" + "0825" + "HAR-ORDER-0001 THANH TOAN", payload);
        Assert.DoesNotContain("DON HANG", payload);
    }

    [Fact]
    public void Transfer_content_drops_special_chars_and_collapses_spaces()
    {
        var payload = _sut.BuildPayload(Bin, Account, 1000, "  HAR#ORDER@0001!!   paid  ")!;

        Assert.Contains("0817HARORDER0001 paid", payload);
    }

    [Fact]
    public void Merchant_name_is_ascii_and_truncated_to_25_chars()
    {
        var payload = _sut.BuildPayload(Bin, Account, 1000, "x", "Công ty TNHH Thương mại Dịch vụ Harian")!;

        Assert.Contains("5925Cong ty TNHH Thuong mai D", payload);
    }

    [Fact]
    public void Merchant_name_falls_back_when_nothing_ascii_remains()
    {
        var payload = _sut.BuildPayload(Bin, Account, 1000, "x", "***")!;

        Assert.Contains("5905KHACH", payload);
    }

    [Fact]
    public void CreatePngDataUrl_returns_png_data_url_or_null()
    {
        Assert.StartsWith("data:image/png;base64,", _sut.CreatePngDataUrl(Bin, Account, 1000, "HAR-ORDER-0001"));
        Assert.Null(_sut.CreatePngDataUrl("1", Account, 1000, "HAR-ORDER-0001"));
    }

    private static List<(string Id, string Value)> ParseTlv(string payload)
    {
        var result = new List<(string, string)>();
        var i = 0;
        while (i < payload.Length)
        {
            var id = payload.Substring(i, 2);
            var len = int.Parse(payload.Substring(i + 2, 2));
            result.Add((id, payload.Substring(i + 4, len)));
            i += 4 + len;
        }
        Assert.Equal(payload.Length, i);
        return result;
    }

    private static string Crc16CcittFalse(string data)
    {
        var crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(data))
        {
            crc ^= b << 8;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
        }
        return crc.ToString("X4");
    }
}
