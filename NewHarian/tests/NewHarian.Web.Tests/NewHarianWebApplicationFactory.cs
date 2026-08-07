using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NewHarian.Web.Tests;

public sealed class NewHarianWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "NewHarianSecurityTests_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting is applied early enough for Program.cs → AddInfrastructure.
        builder.UseSetting("Database:UseInMemory", "true");
        builder.UseSetting("Database:InMemoryName", _dbName);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");
        builder.UseSetting("Email:Smtp:Enabled", "false");
        builder.UseSetting("Email:Smtp:Password", "");
        builder.UseSetting("Email:Smtp:User", "");
        builder.UseSetting("Email:Smtp:From", "test@example.com");
    }
}
