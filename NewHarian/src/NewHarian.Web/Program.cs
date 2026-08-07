using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NewHarian.Infrastructure.DependencyInjection;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
// App logs → AppLogEntries + LogCleanupHostedService (retention 10d / 90d)
builder.Services.AddAppLogging(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<NewHarian.Application.Admin.IAdminNotificationRealtime, NewHarian.Web.Areas.Admin.Services.AdminNotificationRealtime>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddMemoryCache();
builder.Services.AddScoped<NewHarian.Web.Areas.Admin.Services.IProductPreviewStore, NewHarian.Web.Areas.Admin.Services.ProductPreviewStore>();
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Rate limit guest form submits + admin login per IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau một giờ.", ct);
    };

    static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("contact-form", http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));
    options.AddPolicy("careers-form", http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));
    options.AddPolicy("admin-login", http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0
        }));
    options.AddPolicy("checkout-submit", http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));
    options.AddPolicy("booking-submit", http =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = new[] { new CultureInfo("vi"), new CultureInfo("en"), new CultureInfo("ja") };
    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider { CookieName = CookieRequestCultureProvider.DefaultCookieName },
        new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" }
    ];
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage", "?code={0}");
app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.MapControllerRoute(
    name: "productBookThanks",
    pattern: "products/{categorySlug}/{productSlug}/book/thanks",
    defaults: new { controller = "Products", action = "BookThanks" });

app.MapControllerRoute(
    name: "productBook",
    pattern: "products/{categorySlug}/{productSlug}/book",
    defaults: new { controller = "Products", action = "Book" });

app.MapControllerRoute(
    name: "productDetail",
    pattern: "products/{categorySlug}/{productSlug}",
    defaults: new { controller = "Products", action = "Detail" });

app.MapControllerRoute(
    name: "productCategory",
    pattern: "products/{categorySlug}",
    defaults: new { controller = "Products", action = "Category" });

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapHub<NewHarian.Web.Areas.Admin.Hubs.AdminNotificationsHub>("/admin/hubs/notifications");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (!app.Environment.IsEnvironment("Testing"))
    await DbSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;
