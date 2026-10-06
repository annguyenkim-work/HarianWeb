using System.Net;
using Microsoft.Extensions.Configuration;
using NewHarian.Web.Security;

namespace NewHarian.Web.Tests;

public class RateLimitingSetupTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Fact]
    public void Defaults_match_production_limits()
    {
        Assert.Equal((5, TimeSpan.FromHours(1)), RateLimitingSetup.Defaults["contact-form"]);
        Assert.Equal((5, TimeSpan.FromHours(1)), RateLimitingSetup.Defaults["dealers-form"]);
        Assert.Equal((3, TimeSpan.FromHours(1)), RateLimitingSetup.Defaults["careers-form"]);
        Assert.Equal((20, TimeSpan.FromMinutes(15)), RateLimitingSetup.Defaults["admin-login"]);
        Assert.Equal((10, TimeSpan.FromHours(1)), RateLimitingSetup.Defaults["checkout-submit"]);
        Assert.Equal((10, TimeSpan.FromHours(1)), RateLimitingSetup.Defaults["booking-submit"]);
    }

    [Fact]
    public void Missing_config_falls_back_to_default()
    {
        var resolved = RateLimitingSetup.Resolve(Config(), "careers-form", (3, TimeSpan.FromHours(1)));

        Assert.Equal((3, TimeSpan.FromHours(1)), resolved);
    }

    [Fact]
    public void Config_overrides_limit_and_window()
    {
        var config = Config(
            ("RateLimiting:checkout-submit:PermitLimit", "500"),
            ("RateLimiting:checkout-submit:Window", "00:05:00"));

        var resolved = RateLimitingSetup.Resolve(config, "checkout-submit", (10, TimeSpan.FromHours(1)));

        Assert.Equal((500, TimeSpan.FromMinutes(5)), resolved);
    }

    [Fact]
    public void Non_positive_values_are_ignored()
    {
        var config = Config(
            ("RateLimiting:contact-form:PermitLimit", "0"),
            ("RateLimiting:contact-form:Window", "00:00:00"));

        var resolved = RateLimitingSetup.Resolve(config, "contact-form", (5, TimeSpan.FromHours(1)));

        Assert.Equal((5, TimeSpan.FromHours(1)), resolved);
    }

    [Fact]
    public async Task Configured_limit_is_applied_by_the_app()
    {
        using var factory = new NewHarianWebApplicationFactory();
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:admin-login:PermitLimit", "2"));
        var client = configured.CreateClient(new() { AllowAutoRedirect = false });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var html = await (await client.GetAsync("/admin/login")).Content.ReadAsStringAsync();
            var token = AdminLoginHelper.ExtractRequestVerificationToken(html);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = $"limit{i}@example.com",
                ["Password"] = "WrongPassword1",
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = token!
            });
            statuses.Add((await client.PostAsync("/admin/login", form)).StatusCode);
        }

        Assert.NotEqual(HttpStatusCode.TooManyRequests, statuses[1]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
    }
}
