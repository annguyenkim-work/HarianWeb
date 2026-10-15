using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Admin Dealers: modal create/edit, MST uniqueness, license download, per-dealer overrides RBAC. Own fixture (login rate limit).</summary>
public class AdminDealersIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminDealersIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    private Dictionary<string, string> CompanyFields(string token, string taxCode, string name)
    {
        var (province, commune) = DealerTestData.Hanoi(_factory);
        return new()
        {
            ["__RequestVerificationToken"] = token,
            ["BusinessType"] = "Company",
            ["BusinessName"] = name,
            ["TaxCode"] = taxCode,
            ["RepresentativeName"] = "Nguyễn Văn Đại",
            ["RepresentativeTitle"] = "Giám đốc",
            ["FullName"] = "Lê Liên Hệ",
            ["Phone"] = "0912345678",
            ["Email"] = "admin-dealer@test.local",
            ["ProvinceCode"] = province,
            ["CommuneCode"] = commune,
            ["Address"] = "12 Hàng Bài",
            ["ShipToHeadOffice"] = "true",
        };
    }

    [Fact]
    public async Task Sales_manager_creates_dealer_then_edits_tax_code_with_audit()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var form = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/Dealers/Edit"));
        Assert.Contains("id=\"dealer-form\"", form);
        Assert.Contains("name=\"BusinessType\"", form);
        Assert.DoesNotContain("DiscountPercent", form);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(form)!;

        var taxCode = DealerTestData.CompanyTaxCode();
        var name = "Công ty Admin " + DealerTestData.Suffix();
        var created = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Dealers/Save",
            new FormUrlEncodedContent(CompanyFields(token, taxCode, name))));
        Assert.True(created.GetProperty("ok").GetBoolean());
        Assert.Equal("Đã thêm đại lý và gửi mã đại lý qua email.", created.GetProperty("message").GetString());

        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dealer = await db.Dealers.SingleAsync(d => d.TaxCode == taxCode);
            Assert.Equal(DealerStatus.Approved, dealer.Status);
            Assert.Equal(name, dealer.BusinessName);
            Assert.NotNull(dealer.AccessCodeHash);
            Assert.NotNull(dealer.AccessCodeIssuedAt);
            id = dealer.Id;
        }

        var newTaxCode = DealerTestData.CompanyTaxCode();
        var fields = CompanyFields(token, newTaxCode, name);
        fields["Id"] = id.ToString();
        var updated = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Dealers/Save", new FormUrlEncodedContent(fields)));
        Assert.Equal("Đã cập nhật đại lý.", updated.GetProperty("message").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(newTaxCode, (await db.Dealers.SingleAsync(d => d.Id == id)).TaxCode);
            var audit = await db.AuditLogs.Where(a => a.Action == "Dealer.Updated" && a.EntityId == id.ToString()).ToListAsync();
            var entry = Assert.Single(audit);
            Assert.Contains(taxCode, entry.OldValues);
            Assert.Contains(newTaxCode, entry.NewValues);
        }
    }

    [Fact]
    public async Task Save_with_tax_code_of_another_dealer_rerenders_form_with_error()
    {
        var owner = await DealerTestData.DealerAsync(_factory);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var token = await DealerTestData.TokenAsync(client, "/admin/Dealers/Edit");

        var response = await client.PostAsync("/admin/Dealers/Save",
            new FormUrlEncodedContent(CompanyFields(token, owner.TaxCode, "Công ty Trùng MST")));

        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"MST này đã thuộc đại lý #{owner.Id}.", html);
    }

    [Fact]
    public async Task Approve_is_blocked_when_tax_code_already_belongs_to_approved_dealer()
    {
        var taxCode = DealerTestData.CompanyTaxCode();
        var approved = await DealerTestData.DealerAsync(_factory, DealerStatus.Approved, taxCode: taxCode);
        var pending = await DealerTestData.DealerAsync(_factory, DealerStatus.Pending, taxCode: taxCode);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var token = await DealerTestData.TokenAsync(client, "/admin/Dealers");

        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Dealers/Approve",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = pending.Id.ToString(),
            })));

        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal($"MST này đã thuộc đại lý #{approved.Id}.", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Sales_staff_sees_detail_and_license_but_cannot_save_or_edit_overrides()
    {
        var license = await DealerTestData.LicenseAsync(_factory);
        var dealer = await DealerTestData.DealerAsync(_factory, licenseMediaId: license);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff, allowRedirect: false);

        var detail = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Dealers/Detail/{dealer.Id}"));
        Assert.Contains(dealer.BusinessName, detail);
        Assert.Contains(dealer.TaxCode, detail);
        Assert.Contains("data-license-link", detail);
        Assert.DoesNotContain("data-action=\"edit\"", detail);
        Assert.DoesNotContain("data-action=\"edit-overrides\"", detail);
        Assert.DoesNotContain("data-action=\"approve\"", detail);

        var document = await client.GetAsync($"/admin/Dealers/Document/{dealer.Id}");
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Equal("application/pdf", document.Content.Headers.ContentType?.MediaType);

        var token = await DealerTestData.TokenAsync(client, "/admin/Dealers");
        using var save = DealerTestData.Fetch(HttpMethod.Post, "/admin/Dealers/Save",
            new FormUrlEncodedContent(CompanyFields(token, DealerTestData.CompanyTaxCode(), "Không được lưu")));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(save)).StatusCode);

        using var overrides = DealerTestData.Fetch(HttpMethod.Get, $"/admin/Dealers/EditOverrides/{dealer.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(overrides)).StatusCode);
    }

    [Fact]
    public async Task Document_returns_404_without_license_and_requires_login()
    {
        var dealer = await DealerTestData.DealerAsync(_factory);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager, allowRedirect: false);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/admin/Dealers/Document/{dealer.Id}")).StatusCode);

        var licensed = await DealerTestData.DealerAsync(_factory, licenseMediaId: await DealerTestData.LicenseAsync(_factory));
        var guest = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await guest.GetAsync($"/admin/Dealers/Document/{licensed.Id}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sales_manager_saves_dealer_overrides_and_list_shows_badge()
    {
        var (coral, _) = await DealerTestData.ProductAsync(_factory, "Coral", 100_000m);
        var groupId = await DealerTestData.GroupAsync(_factory, "Override " + DealerTestData.Suffix(), [coral], [20m], [25m], [30m]);
        var dealer = await DealerTestData.DealerAsync(_factory);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Dealers/EditOverrides/{dealer.Id}"));
        Assert.Contains("data-override-input", form);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(form)!;

        int tierId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tierId = await db.DealerDiscountTiers.Where(t => t.GroupId == groupId && t.MinQuantity == 20).Select(t => t.Id).SingleAsync();
        }

        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/Dealers/SaveOverrides",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["DealerId"] = dealer.Id.ToString(),
                ["Cells[0].TierId"] = tierId.ToString(),
                ["Cells[0].ProductId"] = coral.ToString(),
                ["Cells[0].Percent"] = "27.5",
            })));
        Assert.True(json.GetProperty("ok").GetBoolean(), json.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cell = await db.DealerDiscountOverrides.SingleAsync(o => o.DealerId == dealer.Id);
            Assert.Equal(27.5m, cell.Percent);
            Assert.Equal(tierId, cell.TierId);
        }

        var list = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Dealers?q={dealer.TaxCode}"));
        Assert.Contains("data-has-overrides", list);
        var detail = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Dealers/Detail/{dealer.Id}"));
        Assert.Contains("data-override-cell", detail);
    }
}
