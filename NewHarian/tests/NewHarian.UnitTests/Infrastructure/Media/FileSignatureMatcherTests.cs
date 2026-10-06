using NewHarian.Infrastructure.Media;

namespace NewHarian.UnitTests.Infrastructure.Media;

[Trait("Category", "Unit")]
public class FileSignatureMatcherTests
{
    [Fact]
    public void Detects_jpeg_png_pdf_signatures()
    {
        Assert.Equal(DetectedFileKind.Jpeg, FileSignatureMatcher.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal(DetectedFileKind.Png, FileSignatureMatcher.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Equal(DetectedFileKind.Pdf, FileSignatureMatcher.Detect("%PDF"u8.ToArray()));
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "Gif")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, "Gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "Webp")]
    [InlineData(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, "Doc")]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "Docx")]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "Unknown")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, "Unknown")]
    [InlineData(new byte[] { 0xFF, 0xD8 }, "Unknown")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "Unknown")]
    [InlineData(new byte[0], "Unknown")]
    public void Detect_identifies_kind_from_magic_bytes(byte[] header, string expected)
    {
        Assert.Equal(expected, FileSignatureMatcher.Detect(header).ToString());
    }

    [Theory]
    [InlineData("Jpeg", ".jpg", true)]
    [InlineData("Jpeg", ".JPEG", true)]
    [InlineData("Jpeg", ".png", false)]
    [InlineData("Png", ".png", true)]
    [InlineData("Pdf", ".pdf", true)]
    [InlineData("Pdf", ".docx", false)]
    [InlineData("Docx", ".docx", true)]
    [InlineData("Docx", ".zip", false)]
    [InlineData("Unknown", ".bin", false)]
    public void ExtensionMatches_requires_extension_consistent_with_content(string kind, string ext, bool expected)
    {
        Assert.Equal(expected, FileSignatureMatcher.ExtensionMatches(Enum.Parse<DetectedFileKind>(kind), ext));
    }

    [Theory]
    [InlineData("Jpeg", "image/jpeg", ".jpg")]
    [InlineData("Png", "image/png", ".png")]
    [InlineData("Webp", "image/webp", ".webp")]
    [InlineData("Pdf", "application/pdf", ".pdf")]
    [InlineData("Unknown", "application/octet-stream", ".bin")]
    public void Content_type_and_extension_follow_detected_kind(string kind, string contentType, string extension)
    {
        var k = Enum.Parse<DetectedFileKind>(kind);

        Assert.Equal(contentType, FileSignatureMatcher.ContentTypeOf(k));
        Assert.Equal(extension, FileSignatureMatcher.PreferredExtension(k));
    }
}
