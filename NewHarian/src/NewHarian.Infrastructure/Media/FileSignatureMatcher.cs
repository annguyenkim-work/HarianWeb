namespace NewHarian.Infrastructure.Media;

internal enum DetectedFileKind
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Webp,
    Pdf,
    Doc,
    Docx
}

/// <summary>Magic-byte sniffing — labels on the box (MIME/ext) are not trusted alone.</summary>
internal static class FileSignatureMatcher
{
    public static DetectedFileKind Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return DetectedFileKind.Jpeg;

        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return DetectedFileKind.Png;

        if (header.Length >= 6
            && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F'
            && header[3] == (byte)'8' && (header[4] == (byte)'7' || header[4] == (byte)'9') && header[5] == (byte)'a')
            return DetectedFileKind.Gif;

        // RIFF....WEBP
        if (header.Length >= 12
            && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
            && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return DetectedFileKind.Webp;

        if (header.Length >= 4
            && header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F')
            return DetectedFileKind.Pdf;

        // OLE Compound File (legacy .doc)
        if (header.Length >= 8
            && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0
            && header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1)
            return DetectedFileKind.Doc;

        // ZIP container (DOCX and others) — refined by extension at call site
        if (header.Length >= 4
            && header[0] == (byte)'P' && header[1] == (byte)'K'
            && (header[2] == 3 || header[2] == 5 || header[2] == 7)
            && (header[3] == 4 || header[3] == 6 || header[3] == 8))
            return DetectedFileKind.Docx;

        return DetectedFileKind.Unknown;
    }

    public static bool ExtensionMatches(DetectedFileKind kind, string ext)
    {
        ext = ext.ToLowerInvariant();
        return kind switch
        {
            DetectedFileKind.Jpeg => ext is ".jpg" or ".jpeg",
            DetectedFileKind.Png => ext == ".png",
            DetectedFileKind.Gif => ext == ".gif",
            DetectedFileKind.Webp => ext == ".webp",
            DetectedFileKind.Pdf => ext == ".pdf",
            DetectedFileKind.Doc => ext == ".doc",
            DetectedFileKind.Docx => ext == ".docx",
            _ => false
        };
    }

    public static string ContentTypeOf(DetectedFileKind kind) => kind switch
    {
        DetectedFileKind.Jpeg => "image/jpeg",
        DetectedFileKind.Png => "image/png",
        DetectedFileKind.Gif => "image/gif",
        DetectedFileKind.Webp => "image/webp",
        DetectedFileKind.Pdf => "application/pdf",
        DetectedFileKind.Doc => "application/msword",
        DetectedFileKind.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };

    public static string PreferredExtension(DetectedFileKind kind) => kind switch
    {
        DetectedFileKind.Jpeg => ".jpg",
        DetectedFileKind.Png => ".png",
        DetectedFileKind.Gif => ".gif",
        DetectedFileKind.Webp => ".webp",
        DetectedFileKind.Pdf => ".pdf",
        DetectedFileKind.Doc => ".doc",
        DetectedFileKind.Docx => ".docx",
        _ => ".bin"
    };
}
