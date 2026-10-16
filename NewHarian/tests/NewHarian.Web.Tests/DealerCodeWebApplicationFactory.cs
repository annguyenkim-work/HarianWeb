using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Email;

namespace NewHarian.Web.Tests;

/// <summary>
/// Reads the plain dealer code from the sensitive email (still queued to the real outbox) and lifts the
/// dealer-code rate limit so functional tests in one fixture do not trip it.
/// </summary>
public sealed class DealerCodeWebApplicationFactory : NewHarianWebApplicationFactory
{
    private static readonly Regex CodePattern = new(@"HRN-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}");

    public ConcurrentQueue<(string To, string Body)> SensitiveMails { get; } = new();

    /// <summary>Latest code mailed to <paramref name="email"/>.</summary>
    public string LastCodeFor(string email)
    {
        var mail = SensitiveMails.LastOrDefault(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
        Assert.False(mail == default, $"No sensitive email to {email}");
        var match = CodePattern.Match(mail.Body);
        Assert.True(match.Success, "Dealer code not found in email body");
        return match.Value;
    }

    protected override void ConfigureTestHost(IWebHostBuilder builder)
        => builder.UseSetting("RateLimiting:dealer-code:PermitLimit", "1000");

    protected override void ConfigureTestServices(IServiceCollection services)
        => services.AddScoped<ISensitiveEmailSender>(sp =>
            new CapturingSender(sp.GetRequiredService<QueuingEmailSender>(), SensitiveMails));

    private sealed class CapturingSender(QueuingEmailSender inner, ConcurrentQueue<(string, string)> sink) : ISensitiveEmailSender
    {
        public Task SendSensitiveAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            sink.Enqueue((to, htmlBody));
            return inner.SendSensitiveAsync(to, subject, htmlBody, ct);
        }
    }
}
