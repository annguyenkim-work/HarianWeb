using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Media;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

public class SecurityIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public SecurityIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Home_response_includes_security_headers()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task Anonymous_cannot_download_cv_endpoint()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/admin/Applications/Cv/1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Private_cv_is_not_under_wwwroot_uploads_and_hr_staff_can_download()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (!await roleManager.RoleExistsAsync(AppRoles.HrStaff))
            await roleManager.CreateAsync(new IdentityRole(AppRoles.HrStaff));

        const string email = "hrstaff-security@test.local";
        const string password = "Staff@12345";
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true
            };
            var create = await userManager.CreateAsync(user, password);
            Assert.True(create.Succeeded, string.Join(";", create.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, AppRoles.HrStaff);
        }

        var media = sp.GetRequiredService<IMediaStorage>();
        await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 security-cv-test"));
        var saved = await media.SaveDocumentAsync(upload, "cv-test.pdf", "application/pdf", user.Id);

        Assert.False(string.IsNullOrWhiteSpace(saved.FileName));
        var mediaRow = await db.MediaFiles.FindAsync(saved.Id);
        Assert.NotNull(mediaRow);
        Assert.True(mediaRow!.IsPrivate);
        Assert.StartsWith("private/", mediaRow.StoredPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/uploads/", mediaRow.StoredPath, StringComparison.OrdinalIgnoreCase);

        var env = sp.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        var publicPath = Path.Combine(env.WebRootPath, "uploads", "applications", Path.GetFileName(mediaRow.StoredPath));
        Assert.False(File.Exists(publicPath));
        var privatePath = MediaPathResolver.GetAbsolutePath(env, mediaRow);
        Assert.True(File.Exists(privatePath));
        Assert.StartsWith(_factory.AppDataContentRoot, privatePath, StringComparison.OrdinalIgnoreCase);

        var application = new JobApplication
        {
            FullName = "Applicant",
            Email = "applicant@test.local",
            Message = "Hello world application note",
            ApplicationType = ApplicationType.Application,
            Status = ApplicationStatus.New,
            LanguageCode = "vi",
            AttachmentMediaFileId = saved.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.JobApplications.Add(application);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });
        await AdminLoginHelper.LoginAsync(client, email, password);

        var download = await client.GetAsync($"/admin/Applications/Cv/{application.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        Assert.Contains("security-cv-test", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }
}
