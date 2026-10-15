using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Address;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Seed helpers for dealer / dealer-discount integration tests (unique data per call).</summary>
internal static class DealerTestData
{
    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Unique 10-digit company MST.</summary>
    public static string CompanyTaxCode() => "0" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    public static (string ProvinceCode, string CommuneCode) Hanoi(NewHarianWebApplicationFactory factory)
    {
        var catalog = factory.Services.GetRequiredService<IVietnamDivisionCatalog>();
        var province = catalog.Provinces.Single(p => p.Code == "01");
        return (province.Code, catalog.CommunesFor(province.Code)[0].Code);
    }

    /// <summary>Guest checkout needs the province in the shipping table (InMemory has no seed).</summary>
    public static async Task EnsureShippingProvinceAsync(NewHarianWebApplicationFactory factory, string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.ShippingProvinces.Any(p => p.Code == code)) return;
        db.ShippingProvinces.Add(new ShippingProvince { Code = code, NameVi = "Thành phố Hà Nội", IsActive = true });
        await db.SaveChangesAsync();
    }

    public static async Task<(int ProductId, string Sku)> ProductAsync(
        NewHarianWebApplicationFactory factory, string name, decimal price)
    {
        using var scope = factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Suffix();
        var product = new Product
        {
            Category = new Category { Slug = $"dd-cat-{suffix}" },
            Slug = $"dd-prod-{suffix}",
            Status = ProductStatus.Published,
            Translations = { new ProductTranslation { LanguageCode = "vi", Name = $"{name} {suffix}" } },
            Variants = { new ProductVariant { Sku = $"DD-{suffix}", VariantLabel = "1kg", Price = price, IsActive = true, StockQuantity = 1000 } }
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, product.Variants.Single().Sku);
    }

    public static async Task<Dealer> DealerAsync(
        NewHarianWebApplicationFactory factory,
        DealerStatus status = DealerStatus.Approved,
        DealerBusinessType type = DealerBusinessType.Company,
        string? taxCode = null,
        int? licenseMediaId = null)
    {
        using var scope = factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (province, commune) = Hanoi(factory);
        var suffix = Suffix();
        var dealer = new Dealer
        {
            BusinessType = type,
            BusinessName = $"Công ty Thử {suffix}",
            TaxCode = taxCode ?? CompanyTaxCode(),
            RepresentativeName = "Nguyễn Văn Đại",
            RepresentativeTitle = type == DealerBusinessType.Company ? "Giám đốc" : null,
            HouseholdRegistrationNo = type == DealerBusinessType.Household ? "01A8000001" : null,
            CitizenId = type == DealerBusinessType.Household ? "001234567890" : null,
            FullName = "Lê Liên Hệ",
            Phone = "0912345678",
            Email = $"dealer-{suffix}@test.local",
            InvoiceEmail = $"invoice-{suffix}@test.local",
            Address = "12 Hàng Bài",
            ProvinceCode = province,
            ProvinceName = "Thành phố Hà Nội",
            CommuneCode = commune,
            CommuneName = "Phường Test",
            Status = status,
            BusinessLicenseMediaFileId = licenseMediaId,
        };
        db.Dealers.Add(dealer);
        await db.SaveChangesAsync();
        return dealer;
    }

    public static async Task<int> LicenseAsync(NewHarianWebApplicationFactory factory, string folder = "dealers")
    {
        using var scope = factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var media = scope.ServiceProvider.GetRequiredService<IMediaStorage>();
        await using var stream = new MemoryStream("%PDF-1.4 business-license"u8.ToArray());
        var saved = await media.SaveDocumentAsync(stream, "gpkd.pdf", "application/pdf", null, default, folder);
        return saved.Id;
    }

    /// <summary>KIRARA-like group: 1-19 / 20-50 / Từ 51 with given % per product (one array per tier).</summary>
    public static async Task<int> GroupAsync(
        NewHarianWebApplicationFactory factory, string name, int[] productIds, params decimal[][] tierPercents)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminDealerDiscountService>();
        var bounds = new (int Min, int? Max)[] { (1, 19), (20, 50), (51, null) };
        var input = new DealerDiscountGroupInput
        {
            Name = name,
            ProductIds = productIds.ToList(),
            Tiers = tierPercents.Select((p, i) => new DealerDiscountTierInput
            {
                MinQuantity = bounds[i].Min,
                MaxQuantity = i == tierPercents.Length - 1 ? null : bounds[i].Max,
                Percents = productIds.Select((pid, j) => (pid, p[j])).ToDictionary(x => x.pid, x => (decimal?)x.Item2),
            }).ToList(),
        };
        var (ok, error, id) = await service.SaveGroupAsync(input, null);
        Assert.True(ok, error);
        return id!.Value;
    }

    public static async Task<string> TokenAsync(HttpClient client, string url)
        => AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync(url))
           ?? throw new InvalidOperationException($"No antiforgery token on {url}");

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/json", body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static HttpRequestMessage Fetch(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        return request;
    }
}
