using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NewHarian.Web.Tests;

public class OpsIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public OpsIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_healthy_json()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Unknown_path_returns_branded_404()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/this-page-definitely-does-not-exist-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.True(
            html.Contains("error-page", StringComparison.OrdinalIgnoreCase)
            && (html.Contains("404", StringComparison.Ordinal)
                || html.Contains("NotFound", StringComparison.OrdinalIgnoreCase)
                || html.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase)
                || html.Contains("page not found", StringComparison.OrdinalIgnoreCase)),
            "Body snippet: " + (html.Length > 500 ? html[..500] : html));
    }

    [Fact]
    public async Task StatusCodePage_renders_404_body()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/Home/StatusCodePage?code=404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("error-page", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("404", html, StringComparison.Ordinal);
    }
}
