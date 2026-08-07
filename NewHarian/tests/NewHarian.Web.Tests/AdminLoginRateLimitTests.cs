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
            var token = ExtractRequestVerificationToken(html);
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

    private static string? ExtractRequestVerificationToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var start = idx + marker.Length;
            var end = html.IndexOf('"', start);
            return end < 0 ? null : html[start..end];
        }

        const string marker2 = "name=\"__RequestVerificationToken\"";
        idx = html.IndexOf(marker2, StringComparison.Ordinal);
        if (idx < 0) return null;
        var valueIdx = html.IndexOf("value=\"", idx, StringComparison.Ordinal);
        if (valueIdx < 0) return null;
        valueIdx += "value=\"".Length;
        var end2 = html.IndexOf('"', valueIdx);
        return end2 < 0 ? null : html[valueIdx..end2];
    }
}
