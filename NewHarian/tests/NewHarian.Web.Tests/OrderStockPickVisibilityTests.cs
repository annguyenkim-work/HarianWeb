using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>
/// FEFO block in order detail: a non-deducted preview needs Orders.PreviewStockPick, actual allocations of a
/// deducted order need Orders.ViewAllocations, Cancelled/Refunded show nothing. Own fixture (login rate limit).
/// </summary>
public class OrderStockPickVisibilityTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private const string PreviewTitle = "Gợi ý lấy hàng (FEFO)";
    private const string DeductedTitle = "Đã lấy hàng (FEFO)";

    private readonly NewHarianWebApplicationFactory _factory;

    public OrderStockPickVisibilityTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Sales_staff_sees_deducted_allocations_but_no_preview_on_non_deducted_orders()
    {
        var orders = await SeedOrdersAsync();
        var sales = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);

        var awaiting = await DetailAsync(sales, orders.AwaitingId);
        Assert.DoesNotContain("order-stock-pick", awaiting);

        var confirmed = await DetailAsync(sales, orders.ConfirmedId);
        Assert.DoesNotContain("order-stock-pick", confirmed);

        var processing = await DetailAsync(sales, orders.ProcessingId);
        Assert.Contains(DeductedTitle, processing);
        Assert.Contains(orders.LotCode, processing);
    }

    [Fact]
    public async Task Sales_manager_sees_preview_on_non_deducted_orders_and_allocations_on_deducted()
    {
        var orders = await SeedOrdersAsync();
        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);

        Assert.Contains(PreviewTitle, await DetailAsync(manager, orders.AwaitingId));
        Assert.Contains(PreviewTitle, await DetailAsync(manager, orders.ConfirmedId));
        Assert.Contains(DeductedTitle, await DetailAsync(manager, orders.ProcessingId));
    }

    [Fact]
    public async Task Cancelled_and_refunded_orders_show_no_fefo_block_even_for_sales_manager()
    {
        var orders = await SeedOrdersAsync();
        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);

        var cancelled = await DetailAsync(manager, orders.CancelledId);
        Assert.Contains(orders.CancelledNumber, cancelled);
        Assert.DoesNotContain("order-stock-pick", cancelled);

        Assert.DoesNotContain("order-stock-pick", await DetailAsync(manager, orders.RefundedId));
    }

    private static async Task<string> DetailAsync(HttpClient client, int orderId)
    {
        var response = await client.GetAsync($"/admin/Orders/Detail?id={orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private sealed record SeededOrders(
        int AwaitingId, int ConfirmedId, int ProcessingId, int CancelledId, string CancelledNumber, int RefundedId, string LotCode);

    private async Task<SeededOrders> SeedOrdersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var category = new Category { Slug = $"fefo-cat-{suffix}" };
        var product = new Product
        {
            Category = category,
            Slug = $"fefo-prod-{suffix}",
            Status = ProductStatus.Published,
            Variants = { new ProductVariant { Sku = $"FEFO-{suffix}", VariantLabel = "1L", Price = 100m, IsActive = true, StockQuantity = 10 } }
        };
        var location = new WarehouseLocation { Code = $"L-{suffix}", Name = "Kho test" };
        db.Products.Add(product);
        db.WarehouseLocations.Add(location);
        await db.SaveChangesAsync();

        var variant = product.Variants.Single();
        var lot = new StockLot
        {
            ProductVariantId = variant.Id,
            WarehouseLocationId = location.Id,
            LotCode = $"LOT-{suffix}",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(365),
            QuantityOnHand = 10
        };
        db.StockLots.Add(lot);

        var awaiting = NewOrder($"T-AW-{suffix}", OrderStatus.AwaitingConfirmation, product, variant);
        var confirmed = NewOrder($"T-CF-{suffix}", OrderStatus.Confirmed, product, variant);
        var processing = NewOrder($"T-PR-{suffix}", OrderStatus.Processing, product, variant);
        processing.StockDeductedAt = DateTime.UtcNow;
        var cancelled = NewOrder($"T-CA-{suffix}", OrderStatus.Cancelled, product, variant);
        var refunded = NewOrder($"T-RF-{suffix}", OrderStatus.Refunded, product, variant);
        db.Orders.AddRange(awaiting, confirmed, processing, cancelled, refunded);
        await db.SaveChangesAsync();

        db.OrderItemLotAllocations.Add(new OrderItemLotAllocation
        {
            OrderItemId = processing.Items.Single().Id,
            StockLotId = lot.Id,
            Quantity = 1
        });
        await db.SaveChangesAsync();

        return new SeededOrders(
            awaiting.Id, confirmed.Id, processing.Id, cancelled.Id, cancelled.OrderNumber, refunded.Id, lot.LotCode!);
    }

    private static Order NewOrder(string number, OrderStatus status, Product product, ProductVariant variant) => new()
    {
        OrderNumber = number,
        CustomerName = "Khách test",
        CustomerEmail = "fefo@test.local",
        ShippingAddress = "1 Test",
        Status = status,
        PaymentMethod = PaymentMethod.COD,
        SubTotal = 100m,
        Total = 100m,
        Items =
        {
            new OrderItem
            {
                ProductId = product.Id,
                ProductVariantId = variant.Id,
                ProductName = "SP test",
                VariantLabel = variant.VariantLabel,
                Sku = variant.Sku,
                UnitPrice = 100m,
                Quantity = 1,
                LineTotal = 100m
            }
        }
    };
}
