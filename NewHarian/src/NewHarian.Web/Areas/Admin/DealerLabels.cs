using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin;

public static class DealerLabels
{
    public static string Status(DealerStatus s) => s switch
    {
        DealerStatus.Pending => "Chờ duyệt",
        DealerStatus.Approved => "Đã duyệt",
        DealerStatus.Rejected => "Từ chối",
        _ => s.ToString()
    };

    public static string Type(DealerBusinessType t)
        => t == DealerBusinessType.Household ? "Hộ kinh doanh" : "Doanh nghiệp";

    public static string TypeShort(DealerBusinessType t)
        => t == DealerBusinessType.Household ? "HKD" : "DN";

    public static string Source(DealerDiscountSource s) => s switch
    {
        DealerDiscountSource.Default => "Mặc định",
        DealerDiscountSource.DealerOverride => "Riêng đại lý",
        DealerDiscountSource.Manual => "Sửa tay",
        _ => "-"
    };
}
