namespace NewHarian.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        // blob: needed for admin image crop/preview (URL.createObjectURL). http: kept until HTTPS+domain.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data: blob: http: https:; " +
            "style-src 'self' 'unsafe-inline' https:; " +
            "script-src 'self' 'unsafe-inline' https:; " +
            "font-src 'self' data: https:; " +
            "frame-ancestors 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        await next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
