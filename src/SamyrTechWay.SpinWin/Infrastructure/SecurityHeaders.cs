namespace SamyrTechWay.SpinWin.Infrastructure;

public static class SecurityHeaders
{
    private const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "media-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseAppSecurityHeaders(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["Content-Security-Policy"] = Csp;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            if (ctx.Request.Path.StartsWithSegments("/admin") || ctx.Request.Path.StartsWithSegments("/api")
                || ctx.Request.Path.StartsWithSegments("/login") || ctx.Request.Path.StartsWithSegments("/play")
                || ctx.Request.Path.StartsWithSegments("/my-prizes"))
            {
                h.CacheControl = "no-store";
            }
            return Task.CompletedTask;
        });
        await next();
    });
}
