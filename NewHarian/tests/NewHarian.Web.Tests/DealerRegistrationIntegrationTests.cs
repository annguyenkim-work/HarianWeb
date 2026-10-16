using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Dealers;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Dealers;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Guest dealer registration: business profile, MST rules, optional license upload. Own fixture (rate limits).</summary>
public class DealerRegistrationIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public DealerRegistrationIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    private DealerFormModel Company(string? taxCode = null)
    {
        var (province, commune) = DealerTestData.Hanoi(_factory);
        return new DealerFormModel
        {
            BusinessType = DealerBusinessType.Company,
            BusinessName = "Công ty TNHH Đăng Ký " + DealerTestData.Suffix(),
            TaxCode = taxCode ?? DealerTestData.CompanyTaxCode(),
            RepresentativeName = "Nguyễn Văn A",
            RepresentativeTitle = "Giám đốc",
            FullName = "Trần Thị B",
            Phone = "0912345678",
            Email = "dk@test.local",
            ProvinceCode = province,
            CommuneCode = commune,
            Address = "12 Hàng Bài",
        };
    }

    [Fact]
    public async Task Company_registers_with_license_and_separate_shipping_address()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var token = await DealerTestData.TokenAsync(client, "/dealers/register");
        var model = Company();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Company"), "BusinessType" },
            { new StringContent(model.BusinessName), "BusinessName" },
            { new StringContent(" " + model.TaxCode + " "), "TaxCode" },
            { new StringContent(model.RepresentativeName), "RepresentativeName" },
            { new StringContent(model.RepresentativeTitle!), "RepresentativeTitle" },
            { new StringContent(model.FullName), "FullName" },
            { new StringContent(model.Phone), "Phone" },
            { new StringContent(model.Email), "Email" },
            { new StringContent(model.ProvinceCode), "ProvinceCode" },
            { new StringContent(model.CommuneCode), "CommuneCode" },
            { new StringContent(model.Address), "Address" },
            { new StringContent("false"), "ShipToHeadOffice" },
            { new StringContent(model.ProvinceCode), "ShippingProvinceCode" },
            { new StringContent(model.CommuneCode), "ShippingCommuneCode" },
            { new StringContent("99 Kho Nhận"), "ShippingAddress" },
            { new StringContent("hoadon@test.local"), "InvoiceEmail" },
        };
        var file = new ByteArrayContent("%PDF-1.4 gpkd"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "businessLicenseFile", "gpkd.pdf");

        var post = await client.PostAsync("/dealers/register", form);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.EndsWith("/dealers/confirm", post.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);

        var confirm = WebUtility.HtmlDecode(await client.GetStringAsync("/dealers/confirm"));
        Assert.Contains(model.BusinessName, confirm);
        Assert.Contains(model.TaxCode, confirm);
        Assert.Contains("gpkd.pdf", confirm);
        Assert.Contains("99 Kho Nhận", confirm);

        using var submitForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AdminLoginHelper.ExtractRequestVerificationToken(confirm)!,
        });
        var submit = await client.PostAsync("/dealers/submit", submitForm);
        Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        Assert.EndsWith("/dealers/thank-you", submit.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Dealers.Include(d => d.BusinessLicense).SingleAsync(d => d.TaxCode == model.TaxCode);
        Assert.Equal(DealerStatus.Pending, stored.Status);
        Assert.Equal(DealerBusinessType.Company, stored.BusinessType);
        Assert.Equal(model.BusinessName, stored.BusinessName);
        Assert.False(stored.ShipToHeadOffice);
        Assert.Equal("99 Kho Nhận", stored.ShippingAddress);
        Assert.Equal("hoadon@test.local", stored.InvoiceEmail);
        Assert.Null(stored.CitizenId);
        Assert.NotNull(stored.BusinessLicense);
        Assert.True(stored.BusinessLicense!.IsPrivate);
        Assert.StartsWith("private/dealers/", stored.BusinessLicense.StoredPath.Replace('\\', '/'));
        Assert.True(await db.AdminNotifications.AnyAsync(n => n.Title.Contains(model.BusinessName)));
    }

    [Fact]
    public async Task Invalid_tax_code_rerenders_form_with_error()
    {
        var client = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(client, "/dealers/register");
        var model = Company("12345");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Company"), "BusinessType" },
            { new StringContent(model.BusinessName), "BusinessName" },
            { new StringContent(model.TaxCode), "TaxCode" },
        };
        var response = await client.PostAsync("/dealers/register", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("MST doanh nghiệp phải gồm 10 chữ số", html);
        Assert.Contains($"value=\"{model.BusinessName}\"", html);
    }

    [Fact]
    public async Task Household_without_owner_citizen_id_is_rejected_by_service()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDealerRegistrationService>();
        var model = Company("001234567890");
        model.BusinessType = DealerBusinessType.Household;
        model.HouseholdRegistrationNo = "01A8000123";
        model.RepresentativeTitle = null;

        Assert.Equal("CCCD chủ hộ phải gồm 9 hoặc 12 chữ số.", await service.ValidateAsync(model));

        model.CitizenId = "001234567890";
        Assert.Null(await service.ValidateAsync(model));
    }

    [Fact]
    public async Task Duplicate_tax_code_is_rejected_but_rejected_dealer_tax_code_can_be_reused()
    {
        var pending = await DealerTestData.DealerAsync(_factory, DealerStatus.Pending);
        var rejected = await DealerTestData.DealerAsync(_factory, DealerStatus.Rejected);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDealerRegistrationService>();

        var duplicate = await service.SubmitAsync(Company(pending.TaxCode), "vi");
        Assert.False(duplicate.Ok);
        Assert.Equal(DealerRegistrationService.DuplicateTaxCodeError, duplicate.Error);

        var reused = await service.SubmitAsync(Company(rejected.TaxCode), "vi");
        Assert.True(reused.Ok, reused.Error);
    }

    [Fact]
    public async Task Foreign_media_id_in_draft_is_not_linked_as_license()
    {
        var foreignMedia = await DealerTestData.LicenseAsync(_factory, folder: "applications");
        var model = Company();
        model.BusinessLicenseMediaFileId = foreignMedia;

        using var scope = _factory.Services.CreateScope();
        var (ok, error, id) = await scope.ServiceProvider.GetRequiredService<IDealerRegistrationService>().SubmitAsync(model, "vi");
        Assert.True(ok, error);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await db.Dealers.SingleAsync(d => d.Id == id)).BusinessLicenseMediaFileId);
    }

    [Fact]
    public async Task Honeypot_submission_is_silently_dropped()
    {
        var model = Company();
        model.Website = "http://spam.example";

        using var scope = _factory.Services.CreateScope();
        var (ok, _, _) = await scope.ServiceProvider.GetRequiredService<IDealerRegistrationService>().SubmitAsync(model, "vi");

        Assert.True(ok);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Dealers.AnyAsync(d => d.TaxCode == model.TaxCode));
    }
}
