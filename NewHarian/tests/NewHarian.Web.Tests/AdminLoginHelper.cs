using System.Net;

namespace NewHarian.Web.Tests;

internal static class AdminLoginHelper
{
    public static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var get = await client.GetAsync("/admin/login");
        var html = await get.Content.ReadAsStringAsync();
        var token = ExtractRequestVerificationToken(html);
        Assert.False(string.IsNullOrEmpty(token));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = token!
        });
        var post = await client.PostAsync("/admin/login", form);
        Assert.True(post.IsSuccessStatusCode || post.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Login failed: {(int)post.StatusCode}");
    }

    public static string? ExtractRequestVerificationToken(string html)
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
