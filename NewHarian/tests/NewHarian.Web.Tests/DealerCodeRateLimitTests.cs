using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Admin;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Isolated factory: the dealer-code limiter (5 / 15 min / IP) is shared by every request of an app instance.</summary>
public class DealerCodeRateLimitTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public DealerCodeRateLimitTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Sixth_attempt_is_blocked_with_localized_json_and_one_admin_alert()
    {
        var guest = _factory.CreateClient();
        var token = await DealerTestData.TokenAsync(guest, "/dealers/register");

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 7; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/checkout/dealer-code")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["code"] = $"HRN-AAAA-BBBB-CCC{i}",
                })
            };
            request.Headers.Add("Accept", "application/json");
            responses.Add(await guest.SendAsync(request));
        }

        Assert.All(responses.Take(5), r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[5].StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[6].StatusCode);
        var json = await DealerTestData.JsonAsync(responses[5]);
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal("Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút.", json.GetProperty("error").GetString());

        using var scope = _factory.Services.CreateScope();
        var alerts = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AdminNotifications
            .CountAsync(n => n.Type == AdminNotificationTypes.DealerCodeAbuse);
        Assert.Equal(1, alerts);
    }
}
