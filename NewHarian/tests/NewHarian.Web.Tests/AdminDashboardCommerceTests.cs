using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Dashboard;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Dashboard;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Commerce KPI / top products / category revenue on the admin dashboard service.</summary>
public class AdminDashboardCommerceTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminDashboardCommerceTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAsync_builds_commerce_kpis_top_products_and_category_revenue()
    {
        var end = AdminDashboardService.TodayLocal();
        var start = end.AddDays(-6);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var category = new Category
            {
                Slug = $"dash-cat-{suffix}",
                Translations = { new CategoryTranslation { LanguageCode = "vi", Name = $"Danh mục Dash {suffix}" } }
            };
            var product = new Product
            {
                Category = category,
                Slug = $"dash-prod-{suffix}",
                Status = ProductStatus.Published,
                Variants =
                {
                    new ProductVariant { Sku = $"DASH-{suffix}", VariantLabel = "1L", Price = 100_000m, IsActive = true }
                }
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var variant = product.Variants.Single();

            // Previous equal-length window: one confirmed order (~10 days ago)
            db.Orders.Add(MakeOrder(
                $"P-{suffix}", OrderStatus.Confirmed, PaymentMethod.COD, OrderSource.Website,
                product, variant, 200_000m, DateTime.UtcNow.AddDays(-10)));

            // Current: 2 confirmed + 1 pending
            db.Orders.Add(MakeOrder(
                $"C1-{suffix}", OrderStatus.Confirmed, PaymentMethod.BankTransfer, OrderSource.Store,
                product, variant, 300_000m, DateTime.UtcNow.AddDays(-2)));
            db.Orders.Add(MakeOrder(
                $"C2-{suffix}", OrderStatus.Delivered, PaymentMethod.COD, OrderSource.Website,
                product, variant, 100_000m, DateTime.UtcNow.AddHours(-2)));
            db.Orders.Add(MakeOrder(
                $"C3-{suffix}", OrderStatus.PendingPayment, PaymentMethod.BankTransfer, OrderSource.Website,
                product, variant, 50_000m, DateTime.UtcNow.AddHours(-1)));
            await db.SaveChangesAsync();
        }

        using var read = _factory.Services.CreateScope();
        var dashboard = read.ServiceProvider.GetRequiredService<IAdminDashboardService>();
        var dto = await dashboard.GetAsync(start, end, includeCharts: true, includeInventory: false);

        Assert.NotNull(dto.Commerce);
        Assert.Equal(400_000m, dto.Commerce.Revenue);
        Assert.Equal(3, dto.Commerce.OrderCount);
        Assert.Equal(2, dto.Commerce.RevenueOrderCount);
        Assert.Equal(200_000m, dto.Commerce.Aov);
        Assert.Equal(100m, dto.Commerce.RevenueDeltaPct);
        Assert.NotNull(dto.OrdersTrendByDay);
        Assert.Equal(7, dto.OrdersTrendByDay.Count);
        Assert.Contains(dto.TopProducts!, p => p.Amount == 400_000m);
        Assert.Contains(dto.RevenueByCategory!, c => c.Amount == 400_000m && c.Label.Contains("Danh mục Dash"));
        Assert.Contains(dto.RevenueByPaymentMethod!, p => p.Key == nameof(PaymentMethod.BankTransfer) && p.Amount == 300_000m);
        Assert.Contains(dto.RevenueByPaymentMethod!, p => p.Key == nameof(PaymentMethod.COD) && p.Amount == 100_000m);
    }

    [Fact]
    public async Task Super_admin_dashboard_html_shows_commerce_kpis_and_trend_chart()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var html = await client.GetStringAsync("/admin");

        Assert.Contains("data-commerce-kpis", html);
        Assert.Contains("data-kpi=\"revenue\"", html);
        Assert.Contains("data-kpi=\"aov\"", html);
        Assert.Contains("chart-orders-trend", html);
        Assert.Contains("chart-top-products", html);
        Assert.Contains("chart-payment-method", html);
        Assert.Contains("data-ops-strip", html);
        Assert.DoesNotContain("chart-gmv", html);
        Assert.DoesNotContain("chart-orders-source", html);
    }

    private static Order MakeOrder(
        string number,
        OrderStatus status,
        PaymentMethod payment,
        OrderSource source,
        Product product,
        ProductVariant variant,
        decimal total,
        DateTime createdAtUtc) => new()
    {
        OrderNumber = number,
        CustomerName = "Khách dash",
        CustomerEmail = $"{number}@test.local",
        ShippingAddress = "HN",
        SubTotal = total,
        Total = total,
        Status = status,
        PaymentMethod = payment,
        Source = source,
        CreatedAt = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
        Items =
        {
            new OrderItem
            {
                ProductId = product.Id,
                ProductVariantId = variant.Id,
                ProductName = $"SP Dash {number}",
                VariantLabel = variant.VariantLabel,
                Sku = variant.Sku,
                UnitPrice = total,
                Quantity = 1,
                LineTotal = total
            }
        }
    };
}
