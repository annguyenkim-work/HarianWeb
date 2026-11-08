using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

/// <summary>Guest badge for an order status. Mid-flow states stay neutral.</summary>
public static class OrderStatusTone
{
    public static string BadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Delivered => "badge-ok",
        OrderStatus.Cancelled or OrderStatus.Refunded => "badge-danger",
        OrderStatus.PendingPayment or OrderStatus.AwaitingConfirmation => "badge-warn",
        _ => "badge-paper"
    };

    public static string LabelKey(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "Orders.Step.PendingPayment",
        OrderStatus.AwaitingConfirmation => "Orders.Step.Awaiting",
        OrderStatus.Confirmed => "Orders.Step.Confirmed",
        OrderStatus.Processing => "Orders.Step.Processing",
        OrderStatus.Shipped => "Orders.Step.Shipped",
        OrderStatus.Delivered => "Orders.Step.Delivered",
        OrderStatus.Cancelled => "Orders.Step.Cancelled",
        OrderStatus.Refunded => "Orders.Step.Refunded",
        _ => "Orders.Status"
    };
}
