using Microsoft.AspNetCore.Mvc.Controllers;
using NewHarian.Application.Analytics;

namespace NewHarian.Web.Middleware;

/// <summary>After a successful guest HTML GET, increments daily pageview rollups (never fails the response).</summary>
public sealed class PageViewMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopes,
    IWebHostEnvironment env,
    ILogger<PageViewMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        try
        {
            var endpoint = context.GetEndpoint();
            var cad = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var kind = PageViewClassifier.Classify(
                context.Request.Method,
                context.Response.StatusCode,
                context.Request.Path.Value ?? "/",
                context.Request.Headers.Accept.ToString(),
                cad?.ControllerName,
                cad?.ActionName,
                cad?.RouteValues.TryGetValue("area", out var area) == true ? area : null);
            if (kind is null) return;

            int? productId = null;
            if (context.Items.TryGetValue(PageViewClassifier.ProductIdItemKey, out var raw) && raw is int id)
                productId = id;

            // Production: fire-and-forget. Testing/E2E: await so assertions see the rollup immediately.
            if (env.IsEnvironment("Testing") || env.IsEnvironment("E2E"))
                await RecordAsync(kind.Value, productId);
            else
                _ = RecordAsync(kind.Value, productId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PageViewMiddleware Error");
        }
    }

    private async Task RecordAsync(Domain.Enums.PageKind kind, int? productId)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPageViewRecorder>().RecordAsync(kind, productId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PageViewMiddleware Record Error PageKind={PageKind}", kind);
        }
    }
}
