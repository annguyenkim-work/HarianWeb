using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

public static class OrderSourceLabels
{
    public static string Vi(OrderSource source) => source switch
    {
        OrderSource.Website => "Website",
        OrderSource.Store => "Cửa hàng",
        OrderSource.Shopee => "Shopee",
        OrderSource.TikTok => "TikTok",
        _ => source.ToString()
    };
}
