using System.Net;

namespace NewHarian.Web.Tests;

/// <summary>Isolated factory so login burst does not affect other security tests.</summary>
public class AdminLoginRateLimitTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdminLoginRateLimitTests(NewHarianWebApplicationFactory factory)
        => _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Admin_login_returns_429_after_burst()
    {
        HttpStatusCode? last = null;
        var sawTooMany = false;
        for (var i = 0; i < 25; i++)
        {
            var get = await _client.GetAsync("/admin/login");
            var html = await get.Content.ReadAsStringAsync();
            var token = AdminLoginHelper.ExtractRequestVerificationToken(html);
            Assert.False(string.IsNullOrEmpty(token));

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = $"attacker{i}@example.com",
                ["Password"] = "WrongPassword1",
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = token!
            });
            var post = await _client.PostAsync("/admin/login", form);
            last = post.StatusCode;
            if (post.StatusCode == HttpStatusCode.TooManyRequests)
            {
                sawTooMany = true;
                break;
            }
        }

        Assert.True(sawTooMany, $"Expected 429 after burst; last status was {last}");
    }
}
