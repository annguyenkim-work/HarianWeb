using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Cart;
using NewHarian.Application.Catalog;
using NewHarian.Application.Cms;
using NewHarian.Application.Dashboard;
using NewHarian.Application.Dealers;
using NewHarian.Application.Email;
using NewHarian.Application.Engagement;
using NewHarian.Application.Orders;
using NewHarian.Application.Posts;
using NewHarian.Application.Shipping;
using NewHarian.Infrastructure.Admin;
using NewHarian.Infrastructure.Audit;
using NewHarian.Infrastructure.Cart;
using NewHarian.Infrastructure.Catalog;
using NewHarian.Infrastructure.Cms;
using NewHarian.Infrastructure.Dashboard;
using NewHarian.Infrastructure.Dealers;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Engagement;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Logging;
using NewHarian.Infrastructure.Media;
using NewHarian.Infrastructure.Orders;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Infrastructure.Posts;
using NewHarian.Infrastructure.Security;
using NewHarian.Infrastructure.Shipping;

namespace NewHarian.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var useInMemory = configuration.GetValue("Database:UseInMemory", false);
        if (useInMemory)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "NewHarianTests"));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found. Set env ConnectionStrings__DefaultConnection (CI/CD) or local override after pull.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/admin/login";
            options.AccessDeniedPath = "/admin/access-denied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // SameAsRequest: works on HTTP (current VPS :5000) and sets Secure automatically over HTTPS.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AdminOnly, p => p.RequireRole(AppRoles.Admin))
            .AddPolicy(AuthorizationPolicies.AdminOrStaff, p => p.RequireRole(AppRoles.Admin, AppRoles.Staff));

        services.AddSingleton<IHtmlContentSanitizer, HtmlContentSanitizer>();
        services.AddScoped<FileLoggingEmailSender>();
        services.AddScoped<ConfigurableEmailSender>();
        services.AddScoped<IEmailSender, QueuingEmailSender>();
        services.AddHostedService<EmailOutboxHostedService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IStatusHistoryService, StatusHistoryService>();
        services.AddScoped<IAdminNotificationRealtime, NullAdminNotificationRealtime>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IServiceBookingService, ServiceBookingService>();
        services.AddScoped<ICartService, SessionCartService>();
        services.AddScoped<IMediaStorage, LocalMediaStorage>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ISiteChromeCache, SiteChromeCache>();
        services.AddScoped<ICmsPageService, CmsPageService>();
        services.AddScoped<IAdminCmsService, AdminCmsService>();
        services.AddScoped<IInquiryService, InquiryService>();
        services.AddScoped<IJobApplicationService, JobApplicationService>();
        services.AddScoped<IDealerService, DealerService>();
        services.AddScoped<ISitePostService, SitePostService>();
        services.AddScoped<IAdminSitePostService, AdminSitePostService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddSingleton<NewHarian.Application.Payments.IVnPayService, NewHarian.Infrastructure.Payments.VnPayService>();
        services.AddSingleton<NewHarian.Application.Payments.IVietQrService, NewHarian.Infrastructure.Payments.VietQrService>();
        services.AddScoped<NewHarian.Application.Payments.IBankTransferDisplayService, NewHarian.Infrastructure.Payments.BankTransferDisplayService>();
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>DB log sink + writer + nightly cleanup (Info 10d / Warn+Error 90d). Call from Program.cs.</summary>
    public static IServiceCollection AddAppLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppLoggingOptions>(configuration.GetSection(AppLoggingOptions.SectionName));
        services.AddSingleton<AppLogQueue>();
        services.AddSingleton<ILoggerProvider, DbLoggerProvider>();
        services.AddHostedService<LogWriterHostedService>();
        services.AddHostedService<LogCleanupHostedService>();
        return services;
    }
}
