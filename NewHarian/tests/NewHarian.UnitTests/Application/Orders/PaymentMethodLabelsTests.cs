using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Orders;

[Trait("Category", "Unit")]
public class PaymentMethodLabelsTests
{
    [Theory]
    [InlineData(PaymentMethod.BankTransfer, "Chuyển khoản")]
    [InlineData(PaymentMethod.COD, "COD")]
    [InlineData(PaymentMethod.OnlineGateway, "Cổng online")]
    [InlineData(PaymentMethod.Offline, "Offline")]
    public void Vi_labels_payment_methods(PaymentMethod method, string expected)
        => Assert.Equal(expected, PaymentMethodLabels.Vi(method));
}
