using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

public static class PaymentMethodLabels
{
    public static string Vi(PaymentMethod method) => method switch
    {
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.COD => "COD",
        PaymentMethod.OnlineGateway => "Cổng online",
        PaymentMethod.Offline => "Offline",
        _ => method.ToString()
    };
}
