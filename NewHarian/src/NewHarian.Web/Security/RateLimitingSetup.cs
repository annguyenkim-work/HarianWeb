using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace NewHarian.Web.Security;

/// <summary>
/// Per-IP fixed-window limits for guest form submits + admin login.
/// Each policy can be overridden via <c>RateLimiting:{policy}:PermitLimit</c> / <c>RateLimiting:{policy}:Window</c> (TimeSpan, e.g. "01:00:00").
/// </summary>
public static class RateLimitingSetup
{
    public const string SectionName = "RateLimiting";

    public static IReadOnlyDictionary<string, (int PermitLimit, TimeSpan Window)> Defaults { get; } =
        new Dictionary<string, (int, TimeSpan)>(StringComparer.Ordinal)
        {
            ["contact-form"] = (5, TimeSpan.FromHours(1)),
            ["dealers-form"] = (5, TimeSpan.FromHours(1)),
            ["careers-form"] = (3, TimeSpan.FromHours(1)),
            ["admin-login"] = (20, TimeSpan.FromMinutes(15)),
            ["checkout-submit"] = (10, TimeSpan.FromHours(1)),
            ["booking-submit"] = (10, TimeSpan.FromHours(1)),
        };

    public static IServiceCollection AddNewHarianRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await context.HttpContext.Response.WriteAsync(
                    "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau một giờ.", ct);
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

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
