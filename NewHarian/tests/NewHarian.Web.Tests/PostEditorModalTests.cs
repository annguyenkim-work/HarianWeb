using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Tests;

/// <summary>Post edit stays on the list and opens in a modal. Own fixture (login rate limit).</summary>
public class PostEditorModalTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public PostEditorModalTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Job_list_opens_the_editor_in_a_modal_instead_of_a_page()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);

        var list = await client.GetStringAsync("/admin/Posts?kind=Job");
        Assert.Contains("id=\"post-modal\"", list);
        Assert.Contains("id=\"btn-add-post\"", list);
        Assert.DoesNotContain("href=\"/admin/Posts/Edit", list, StringComparison.OrdinalIgnoreCase);

        var form = await client.GetStringAsync("/admin/Posts/Edit?kind=Job");
        Assert.Contains("id=\"post-form\"", form);
        Assert.Contains("richtext", form);
        Assert.DoesNotContain("admin-sidebar", form);
    }
}
