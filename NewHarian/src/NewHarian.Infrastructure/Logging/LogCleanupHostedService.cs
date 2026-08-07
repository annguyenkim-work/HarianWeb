using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Logging;

public sealed class LogCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<AppLoggingOptions> options,
    ILogger<LogCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "App log cleanup failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var infoDays = Math.Max(1, opts.RetainInformationDays);
        var warnDays = Math.Max(infoDays, opts.RetainWarningErrorDays);

        var infoCutoff = DateTime.UtcNow.AddDays(-infoDays);
        var warnCutoff = DateTime.UtcNow.AddDays(-warnDays);
        // Information = 2; Warning = 3
        const short information = 2;
        const short warning = 3;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var infoDeleted = await db.AppLogEntries
            .Where(e => e.Level <= information && e.CreatedAtUtc < infoCutoff)
            .ExecuteDeleteAsync(ct);

        var warnDeleted = await db.AppLogEntries
            .Where(e => e.Level >= warning && e.CreatedAtUtc < warnCutoff)
            .ExecuteDeleteAsync(ct);

        if (infoDeleted > 0 || warnDeleted > 0)
            logger.LogInformation(
                "App log cleanup Done InfoDeleted={InfoDeleted} WarnErrorDeleted={WarnDeleted}",
                infoDeleted, warnDeleted);
    }
}
