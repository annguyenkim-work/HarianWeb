using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using NewHarian.Application.Admin;

namespace NewHarian.Web.Security;

/// <summary>
/// Per-IP fixed-window limits for guest form submits + admin login.
/// Each policy can be overridden via <c>RateLimiting:{policy}:PermitLimit</c> / <c>RateLimiting:{policy}:Window</c> (TimeSpan, e.g. "01:00:00").
/// </summary>
public static class RateLimitingSetup
{
    public const string SectionName = "RateLimiting";
    public const string DealerCodePolicy = "dealer-code";

    public static IReadOnlyDictionary<string, (int PermitLimit, TimeSpan Window)> Defaults { get; } =
        new Dictionary<string, (int, TimeSpan)>(StringComparer.Ordinal)
        {
            ["contact-form"] = (5, TimeSpan.FromHours(1)),
            ["dealers-form"] = (5, TimeSpan.FromHours(1)),
            ["dealers-upload"] = (10, TimeSpan.FromHours(1)),
            ["careers-form"] = (3, TimeSpan.FromHours(1)),
            ["admin-login"] = (20, TimeSpan.FromMinutes(15)),
            ["checkout-submit"] = (10, TimeSpan.FromHours(1)),
            ["booking-submit"] = (10, TimeSpan.FromHours(1)),
            [DealerCodePolicy] = (5, TimeSpan.FromMinutes(15)),
        };

    public static IServiceCollection AddNewHarianRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                if (policy == DealerCodePolicy)
                {
                    await NotifyDealerCodeAbuseAsync(http, ct);
                    var message = http.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>()
                        ["Checkout.DealerCode.TooMany"].Value;
                    if (WantsJson(http.Request))
                    {
                        await http.Response.WriteAsJsonAsync(new { ok = false, error = message }, ct);
                        return;
                    }
                    http.Response.ContentType = "text/plain; charset=utf-8";
                    await http.Response.WriteAsync(message, ct);
                    return;
                }

                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync("Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau một giờ.", ct);
            };

            foreach (var (policy, fallback) in Defaults)
            {
                var (permitLimit, window) = Resolve(configuration, policy, fallback);
                options.AddPolicy(policy, http =>
                    RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0
                    }));
            }
        });
    }

    public static (int PermitLimit, TimeSpan Window) Resolve(
        IConfiguration configuration, string policy, (int PermitLimit, TimeSpan Window) fallback)
    {
        var section = configuration.GetSection($"{SectionName}:{policy}");
        var permitLimit = section.GetValue<int?>("PermitLimit") is int p && p > 0 ? p : fallback.PermitLimit;
        var window = section.GetValue<TimeSpan?>("Window") is TimeSpan w && w > TimeSpan.Zero ? w : fallback.Window;
        return (permitLimit, window);
    }

    public static bool WantsJson(HttpRequest request)
        => request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>At most one admin alert per IP per hour; never blocks the 429 response.</summary>
    private static async Task NotifyDealerCodeAbuseAsync(HttpContext http, CancellationToken ct)
    {
        var services = http.RequestServices;
        var ip = ClientIp(http);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RateLimitingSetup));
        logger.LogWarning("VerifyDealerCode Done rejected RateLimited Ip={Ip}", ip);
        try
        {
            var cache = services.GetRequiredService<IMemoryCache>();
            if (cache.TryGetValue($"dealer-code-abuse:{ip}", out _)) return;
            cache.Set($"dealer-code-abuse:{ip}", true, TimeSpan.FromHours(1));
            await services.GetRequiredService<IAdminNotificationService>().PublishAsync(
                AdminNotificationTypes.DealerCodeAbuse,
                "Nhiều lần nhập sai mã đại lý",
                $"IP {ip} đã bị chặn tạm thời khi nhập mã đại lý.",
                "/admin/Dealers",
                "Dealer",
                null,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dealer code abuse notification failed Ip={Ip}", ip);
        }
    }

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
