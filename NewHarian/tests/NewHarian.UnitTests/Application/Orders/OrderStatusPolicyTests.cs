using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Orders;

[Trait("Category", "Unit")]
public class OrderStatusPolicyTests
{
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> Allowed =
    [
        (OrderStatus.Confirmed, OrderStatus.Processing),
        (OrderStatus.Processing, OrderStatus.Shipped),
        (OrderStatus.Shipped, OrderStatus.Delivered),
        (OrderStatus.AwaitingConfirmation, OrderStatus.Cancelled),
        (OrderStatus.PendingPayment, OrderStatus.Cancelled),
        (OrderStatus.Confirmed, OrderStatus.Cancelled),
        (OrderStatus.Processing, OrderStatus.Cancelled),
    ];

    public static TheoryData<OrderStatus, OrderStatus, bool> TransitionMatrix()
    {
        var data = new TheoryData<OrderStatus, OrderStatus, bool>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        foreach (var to in Enum.GetValues<OrderStatus>())
            data.Add(from, to, Allowed.Contains((from, to)));
        return data;
    }

    [Theory]
    [MemberData(nameof(TransitionMatrix))]
    public void CanTransition_matches_full_matrix(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStatusPolicy.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Refunded)]
    public void Terminal_statuses_have_no_outgoing_transition(OrderStatus from)
    {
        Assert.All(Enum.GetValues<OrderStatus>(), to => Assert.False(OrderStatusPolicy.CanTransition(from, to)));
    }

    [Fact]
    public void Admin_cannot_confirm_payment_through_generic_status_update()
    {
        Assert.False(OrderStatusPolicy.CanTransition(OrderStatus.PendingPayment, OrderStatus.Confirmed));
        Assert.False(OrderStatusPolicy.CanTransition(OrderStatus.AwaitingConfirmation, OrderStatus.Confirmed));
    }

    [Fact]
    public void CancellableStatuses_are_everything_before_shipping()
    {
        Assert.True(OrderStatusPolicy.CancellableStatuses.SetEquals(
        [
            OrderStatus.AwaitingConfirmation, OrderStatus.PendingPayment, OrderStatus.Confirmed, OrderStatus.Processing
        ]));
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, false)]
    [InlineData(OrderStatus.AwaitingConfirmation, false)]
    [InlineData(OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Refunded, false)]
    public void RequiresStockDeduction_from_processing_onwards(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderStatusPolicy.RequiresStockDeduction(status));
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, true)]
    [InlineData(OrderStatus.AwaitingConfirmation, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Refunded, false)]
    public void NeedsStockPick_except_cancelled_or_refunded(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderStatusPolicy.NeedsStockPick(status));
    }

    [Fact]
    public void NeedsStockPick_covers_every_status()
    {
        var hidden = Enum.GetValues<OrderStatus>().Where(s => !OrderStatusPolicy.NeedsStockPick(s));
        Assert.Equal([OrderStatus.Cancelled, OrderStatus.Refunded], hidden.Order());
    }

    public static TheoryData<OrderStatus, PaymentMethod> AllStatusMethodPairs()
    {
        var data = new TheoryData<OrderStatus, PaymentMethod>();
        foreach (var status in Enum.GetValues<OrderStatus>())
        foreach (var method in Enum.GetValues<PaymentMethod>())
            data.Add(status, method);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatusMethodPairs))]
    public void CanConfirmCod_only_for_cod_awaiting_confirmation(OrderStatus status, PaymentMethod method)
    {
        var expected = status == OrderStatus.AwaitingConfirmation && method == PaymentMethod.COD;
        Assert.Equal(expected, OrderStatusPolicy.CanConfirmCod(status, method));
    }

    [Theory]
    [MemberData(nameof(AllStatusMethodPairs))]
    public void CanConfirmBankTransfer_only_for_bank_transfer_pending_payment(OrderStatus status, PaymentMethod method)
    {
        var expected = status == OrderStatus.PendingPayment && method == PaymentMethod.BankTransfer;
        Assert.Equal(expected, OrderStatusPolicy.CanConfirmBankTransfer(status, method));
    }
}
