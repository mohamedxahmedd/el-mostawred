using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SamyrTechWay.SpinWin.Infrastructure;

public static class RateLimits
{
    public const string Login = "login";
    public const string Spin = "spin";

    /// <summary>Permits per minute. Configurable via RateLimiting:LoginPerMinute / RateLimiting:SpinPerMinute.</summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var loginPermits = config.GetValue("RateLimiting:LoginPerMinute", 10);
        var spinPermits = config.GetValue("RateLimiting:SpinPerMinute", 20);
        return services.AddRateLimiter(o =>
        {
            o.AddPolicy(Login, ctx => RateLimitPartition.GetFixedWindowLimiter(
                "login:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            o.AddPolicy(Spin, ctx => RateLimitPartition.GetFixedWindowLimiter(
                "spin:" + (ctx.User.CustomerId()?.ToString() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = spinPermits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            o.OnRejected = async (ctx, ct) =>
            {
                var http = ctx.HttpContext;
                var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimit");
                logger.LogWarning("Rate limit hit on {Path} from {Ip}", http.Request.Path, http.Connection.RemoteIpAddress);

                if (http.Request.Path.StartsWithSegments("/api"))
                {
                    http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    http.Response.Headers.RetryAfter = "30";
                    await http.Response.WriteAsJsonAsync(new { error = "rateLimited" }, ct);
                    return;
                }
                var target = http.Request.Path.StartsWithSegments("/admin") ? "/admin/login?e=rate" : "/login?e=rate";
                http.Response.StatusCode = StatusCodes.Status303SeeOther;
                http.Response.Headers.Location = target;
            };
        });
    }
}
