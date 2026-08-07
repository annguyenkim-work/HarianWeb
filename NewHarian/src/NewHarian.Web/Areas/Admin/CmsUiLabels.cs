using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin;

public static class CmsUiLabels
{
    public static string BlockType(ContentBlockType type) => type switch
    {
        ContentBlockType.RichText => "Văn bản",
        ContentBlockType.TextWithImage => "Văn bản và hình ảnh",
        ContentBlockType.DataTable => "Bảng thông tin",
        ContentBlockType.CtaButton => "Nút liên kết",
        ContentBlockType.BulletList => "Danh sách (cũ)",
        ContentBlockType.ImageGallery => "Thư viện ảnh (cũ)",
        ContentBlockType.ZigzagFeature => "Zigzag (cũ)",
        _ => type.ToString()
    };

    public static bool IsSupported(ContentBlockType type)
        => type is ContentBlockType.RichText
            or ContentBlockType.TextWithImage
            or ContentBlockType.DataTable
            or ContentBlockType.CtaButton;

    public static string Module(string code) => code switch
    {
        "home" => "Trang chủ",
        "about" => "Giới thiệu",
        "company" => "Công ty",
        "legal" => "Pháp lý",
        "contact" => "Liên hệ",
        _ => code
    };
}
