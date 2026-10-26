using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Bảng chiết khấu đại lý mặc định: modal group CRUD, decimal binding, RBAC, override cleanup. Own fixture.</summary>
public class DealerDiscountsIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public DealerDiscountsIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Sales_manager_creates_group_with_decimal_percent_and_overview_lists_it()
    {
        var (coral, _) = await DealerTestData.ProductAsync(_factory, "Coral", 100_000m);
        var (mau, _) = await DealerTestData.ProductAsync(_factory, "Màu", 80_000m);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/DealerDiscounts/Edit"));
        Assert.Contains("id=\"discount-group-form\"", form);
        Assert.Contains($"value=\"{coral}\"", form);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(form)!;
        var name = "KIRARA " + DealerTestData.Suffix();

        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Name", name),
            new("ProductVariantIds", coral.ToString()),
            new("ProductVariantIds", mau.ToString()),
        };
        var tiers = new (int Min, string Max, string Coral, string Mau)[] { (1, "19", "20", "18"), (20, "50", "25", "22.5"), (51, "", "30", "27") };
        for (var i = 0; i < tiers.Length; i++)
        {
            fields.Add(new($"Tiers[{i}].MinQuantity", tiers[i].Min.ToString()));
            fields.Add(new($"Tiers[{i}].MaxQuantity", tiers[i].Max));
            fields.Add(new($"Tiers[{i}].Percents[{coral}]", tiers[i].Coral));
            fields.Add(new($"Tiers[{i}].Percents[{mau}]", tiers[i].Mau));
        }

        var json = await DealerTestData.JsonAsync(await client.PostAsync("/admin/DealerDiscounts/Save", new FormUrlEncodedContent(fields)));
        Assert.True(json.GetProperty("ok").GetBoolean(), json.ToString());
        Assert.Equal("Đã thêm nhóm chiết khấu.", json.GetProperty("message").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = await db.DealerDiscountGroups.Include(g => g.Tiers).ThenInclude(t => t.Rates).SingleAsync(g => g.Name == name);
            var tier2 = group.Tiers.Single(t => t.MinQuantity == 20);
            Assert.Equal(50, tier2.MaxQuantity);
            Assert.Equal(22.5m, tier2.Rates.Single(r => r.ProductVariantId == mau).Percent);
            Assert.Null(group.Tiers.Single(t => t.MinQuantity == 51).MaxQuantity);
        }

        var index = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/DealerDiscounts"));
        Assert.Contains(name, index);
        Assert.Contains("id=\"btn-add-group\"", index);
        Assert.Contains("Từ 51", index);
    }

    [Fact]
    public async Task Product_already_in_another_group_is_rejected_with_form_error()
    {
        var (coral, _) = await DealerTestData.ProductAsync(_factory, "Taken", 100_000m);
        var owner = "Owner " + DealerTestData.Suffix();
        await DealerTestData.GroupAsync(_factory, owner, [coral], [10m]);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var token = await DealerTestData.TokenAsync(client, "/admin/DealerDiscounts/Edit");

        var response = await client.PostAsync("/admin/DealerDiscounts/Save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Second " + DealerTestData.Suffix(),
            ["ProductVariantIds"] = coral.ToString(),
            ["Tiers[0].MinQuantity"] = "1",
            ["Tiers[0].MaxQuantity"] = "",
            [$"Tiers[0].Percents[{coral}]"] = "5",
        }));

        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"đã thuộc nhóm '{owner}'", html);
    }

    [Fact]
    public async Task Move_and_delete_group_via_post()
    {
        var (p1, _) = await DealerTestData.ProductAsync(_factory, "MoveA", 1000m);
        var (p2, _) = await DealerTestData.ProductAsync(_factory, "MoveB", 1000m);
        var first = await DealerTestData.GroupAsync(_factory, "MoveA " + DealerTestData.Suffix(), [p1], [1m]);
        var second = await DealerTestData.GroupAsync(_factory, "MoveB " + DealerTestData.Suffix(), [p2], [1m]);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager, allowRedirect: false);
        var token = await DealerTestData.TokenAsync(client, "/admin/DealerDiscounts");

        var move = await client.PostAsync("/admin/DealerDiscounts/Move", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["id"] = second.ToString(), ["direction"] = "-1",
        }));
        Assert.Equal(HttpStatusCode.Redirect, move.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sorts = await db.DealerDiscountGroups.Where(g => g.Id == first || g.Id == second).ToDictionaryAsync(g => g.Id, g => g.SortOrder);
            Assert.True(sorts[second] < sorts[first]);
        }

        var delete = await client.PostAsync("/admin/DealerDiscounts/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["id"] = first.ToString(),
        }));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.DealerDiscountGroups.AnyAsync(g => g.Id == first));
            Assert.False(await db.DealerDiscountGroupVariants.AnyAsync(g => g.ProductVariantId == p1));
        }
    }

    [Fact]
    public async Task Sales_staff_views_table_read_only_and_cannot_save()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff, allowRedirect: false);

        var index = await client.GetAsync("/admin/DealerDiscounts");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var html = await index.Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"btn-add-group\"", html);
        Assert.DoesNotContain("class=\"btn-icon btn-edit-group\"", html);
        Assert.Contains("href=\"/admin/DealerDiscounts\"", html, StringComparison.OrdinalIgnoreCase);

        var token = AdminLoginHelper.ExtractRequestVerificationToken(html) ?? await DealerTestData.TokenAsync(client, "/admin/MyProfile");
        using var save = DealerTestData.Fetch(HttpMethod.Post, "/admin/DealerDiscounts/Save",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Name"] = "Không" }));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(save)).StatusCode);

        using var edit = DealerTestData.Fetch(HttpMethod.Get, "/admin/DealerDiscounts/Edit");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(edit)).StatusCode);
    }

    [Fact]
    public async Task Removing_tier_or_product_deletes_matching_dealer_overrides_only()
    {
        var (a, _) = await DealerTestData.ProductAsync(_factory, "OvA", 1000m);
        var (b, _) = await DealerTestData.ProductAsync(_factory, "OvB", 1000m);
        var groupId = await DealerTestData.GroupAsync(_factory, "Ov " + DealerTestData.Suffix(), [a, b], [10m, 10m], [20m, 20m], [30m, 30m]);
        var dealer = await DealerTestData.DealerAsync(_factory);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tiers = await db.DealerDiscountTiers.Where(t => t.GroupId == groupId).OrderBy(t => t.MinQuantity).ToListAsync();
        var overrides = scope.ServiceProvider.GetRequiredService<IDealerDiscountOverrideService>();
        var saved = await overrides.SaveAsync(new DealerDiscountOverridesInput
        {
            DealerId = dealer.Id,
            Cells =
            [
                new() { TierId = tiers[0].Id, ProductVariantId = a, Percent = 15 },
                new() { TierId = tiers[2].Id, ProductVariantId = a, Percent = 35 },
                new() { TierId = tiers[0].Id, ProductVariantId = b, Percent = 12 },
            ],
        }, null);
        Assert.True(saved.Ok, saved.Error);

        var service = scope.ServiceProvider.GetRequiredService<IAdminDealerDiscountService>();
        var edit = await service.GetGroupEditAsync(groupId);
        Assert.Equal(1, edit.DealersWithOverrides);

        var input = edit.Input;
        input.ProductVariantIds = [a];
        input.Tiers = input.Tiers.Take(2).ToList();
        input.Tiers[1].MaxQuantity = null;
        foreach (var t in input.Tiers) t.Percents.Remove(b);
        input.Tiers[0].Percents[a] = 11;
        var result = await service.SaveGroupAsync(input, null);
        Assert.True(result.Ok, result.Error);

        using var verify = _factory.Services.CreateScope();
        var left = await verify.ServiceProvider.GetRequiredService<AppDbContext>().DealerDiscountOverrides
            .Where(o => o.DealerId == dealer.Id).ToListAsync();
        var cell = Assert.Single(left);
        Assert.Equal((tiers[0].Id, a, 15m), (cell.TierId, cell.ProductVariantId, cell.Percent));
    }

    [Fact]
    public async Task Overview_flags_published_skus_without_group()
    {
        var (lonely, _) = await DealerTestData.ProductAsync(_factory, "Lonely", 1000m);
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/DealerDiscounts"));

        Assert.Contains("data-unconfigured", html);
        Assert.Contains($"data-variant-id=\"{lonely}\"", html);
        Assert.Contains("SKU chưa có chiết khấu đại lý", html);
    }
}
