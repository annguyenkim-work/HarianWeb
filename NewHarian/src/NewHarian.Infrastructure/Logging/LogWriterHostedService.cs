using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Logging;

public sealed class LogWriterHostedService(
    AppLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<LogWriterHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AppLogEntry>(100);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();
                while (batch.Count < 100 && queue.Reader.TryRead(out var entry))
                    batch.Add(entry);

                if (batch.Count == 0)
                {
                    try
                    {
                        var next = await queue.Reader.ReadAsync(stoppingToken);
                        batch.Add(next);
                        while (batch.Count < 100 && queue.Reader.TryRead(out var more))
                            batch.Add(more);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.AppLogEntries.AddRange(batch);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Avoid flooding: write to console logger only (this category is filtered from DB)
                logger.LogError(ex, "Failed to persist app log batch ({Count} entries)", batch.Count);
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
