using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

/// <summary>Admin order workflow: Confirmed → Processing → Shipped → Delivered; cancel before shipping.</summary>
public static class OrderStatusPolicy
{
    public static IReadOnlySet<OrderStatus> CancellableStatuses { get; } = new HashSet<OrderStatus>
    {
        OrderStatus.AwaitingConfirmation,
        OrderStatus.PendingPayment,
        OrderStatus.Confirmed,
        OrderStatus.Processing
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to)
    {
        if (to == OrderStatus.Cancelled)
            return CancellableStatuses.Contains(from);

        return (from, to) switch
        {
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false
        };
    }

    /// <summary>FEFO stock is issued once the order reaches Processing or later.</summary>
    public static bool RequiresStockDeduction(OrderStatus status)
        => status is OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered;

    /// <summary>FEFO pick block (suggestion or picked lots) is pointless once an order is cancelled or refunded.</summary>
    public static bool NeedsStockPick(OrderStatus status)
        => status is not (OrderStatus.Cancelled or OrderStatus.Refunded);

    public static bool CanConfirmCod(OrderStatus status, PaymentMethod method)
        => method == PaymentMethod.COD && status == OrderStatus.AwaitingConfirmation;

    public static bool CanConfirmBankTransfer(OrderStatus status, PaymentMethod method)
        => method == PaymentMethod.BankTransfer && status == OrderStatus.PendingPayment;
}
