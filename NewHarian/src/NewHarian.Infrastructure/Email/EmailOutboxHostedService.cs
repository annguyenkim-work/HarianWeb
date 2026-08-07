using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Email;

public sealed class EmailOutboxHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailOutboxHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmailOutboxHostedService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "EmailOutbox poll error");
            }

            try
            {
                await Task.Delay(processed > 0 ? PollDelay : IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var smtp = scope.ServiceProvider.GetRequiredService<ConfigurableEmailSender>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var now = DateTime.UtcNow;
        var batch = await db.EmailOutboxMessages
            .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.Id)
            .Take(10)
            .ToListAsync(ct);

        if (batch.Count == 0) return 0;

        foreach (var msg in batch)
        {
            msg.Status = EmailOutboxStatus.Processing;
            msg.ProcessedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        var sent = 0;
        foreach (var msg in batch)
        {
            try
            {
                var attachments = LoadAttachments(env, msg.AttachmentsJson);
                await smtp.SendAsync(msg.ToAddress, msg.Subject, msg.HtmlBody, ct, attachments);
                msg.Status = EmailOutboxStatus.Sent;
                msg.SentAt = DateTime.UtcNow;
                msg.LastError = null;
                sent++;
                logger.LogInformation("EmailOutbox Sent Id={Id} To={To}", msg.Id, msg.ToAddress);
            }
            catch (Exception ex)
            {
                msg.AttemptCount++;
                msg.LastError = Truncate(ex.Message, 2000);
                if (msg.AttemptCount >= msg.MaxAttempts)
                {
                    msg.Status = EmailOutboxStatus.Failed;
                    logger.LogError(ex, "EmailOutbox Failed Id={Id} after {Attempts} attempts", msg.Id, msg.AttemptCount);
                }
                else
                {
                    msg.Status = EmailOutboxStatus.Pending;
                    var delaySec = Math.Min(300, (int)Math.Pow(2, msg.AttemptCount) * 5); // 10s, 20s, 40s…
                    msg.NextAttemptAt = DateTime.UtcNow.AddSeconds(delaySec);
                    logger.LogWarning(ex, "EmailOutbox retry Id={Id} Attempt={Attempt} Next={Next}",
                        msg.Id, msg.AttemptCount, msg.NextAttemptAt);
                }
            }
            await db.SaveChangesAsync(ct);
        }

        return sent;
    }

    private static IReadOnlyList<EmailAttachment>? LoadAttachments(IWebHostEnvironment env, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        List<QueuingEmailSender.StoredAttachmentMeta>? metas;
        try
        {
            metas = JsonSerializer.Deserialize<List<QueuingEmailSender.StoredAttachmentMeta>>(json, JsonOpts);
        }
        catch
        {
            return null;
        }

        if (metas is null || metas.Count == 0) return null;
        var list = new List<EmailAttachment>();
        foreach (var m in metas)
        {
            var path = Path.Combine(env.ContentRootPath, "App_Data", m.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            list.Add(new EmailAttachment(m.FileName, m.ContentType, File.ReadAllBytes(path)));
        }
        return list.Count == 0 ? null : list;
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);
}
