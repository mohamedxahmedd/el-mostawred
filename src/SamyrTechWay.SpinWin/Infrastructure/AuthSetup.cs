using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Infrastructure;

public static class AuthSchemes
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}

public static class Policies
{
    public const string Customer = "CustomerOnly";
    public const string Admin = "AdminOnly";
}

public static class AuthSetup
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, bool requireHttps)
    {
        var secure = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

        services.AddAuthentication(AuthSchemes.Customer)
            .AddCookie(AuthSchemes.Customer, o =>
            {
                o.Cookie.Name = ".stw.customer";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = secure;
                o.Cookie.Path = "/";
                o.LoginPath = "/login";
                o.LogoutPath = "/logout";
                o.AccessDeniedPath = "/error/403";
                o.ExpireTimeSpan = TimeSpan.FromDays(2);
                o.SlidingExpiration = true;
                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx => ApiAwareRedirect(ctx, StatusCodes.Status401Unauthorized, "unauthenticated"),
                    OnRedirectToAccessDenied = ctx => ApiAwareRedirect(ctx, StatusCodes.Status403Forbidden, "forbidden"),
                    OnValidatePrincipal = ValidateCustomerAsync,
                };
            })
            .AddCookie(AuthSchemes.Admin, o =>
            {
                o.Cookie.Name = ".stw.admin";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = secure;
                o.Cookie.Path = "/admin";
                o.LoginPath = "/admin/login";
                o.LogoutPath = "/admin/logout";
                o.AccessDeniedPath = "/error/403";
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                o.Events = new CookieAuthenticationEvents { OnValidatePrincipal = ValidateAdminAsync };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Customer, p => p
                .AddAuthenticationSchemes(AuthSchemes.Customer)
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaims.Role, AppClaims.CustomerRole))
            .AddPolicy(Policies.Admin, p => p
                .AddAuthenticationSchemes(AuthSchemes.Admin)
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaims.Role, AppClaims.AdminRole));

        return services;
    }

    public static ClaimsPrincipal CustomerPrincipal(Customer c) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, c.Id.ToString()),
        new Claim(ClaimTypes.GivenName, c.FirstName),
        new Claim(AppClaims.Role, AppClaims.CustomerRole),
        new Claim(AppClaims.AccessCodeVersion, c.AccessCodeVersion.ToString()),
    ], AuthSchemes.Customer));

    public static ClaimsPrincipal AdminPrincipal(AdminUser a) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, a.Id),
        new Claim(ClaimTypes.Name, a.Email ?? a.UserName ?? a.Id),
        new Claim(AppClaims.DisplayName, string.IsNullOrWhiteSpace(a.DisplayName) ? a.Email ?? "admin" : a.DisplayName),
        new Claim(AppClaims.Role, AppClaims.AdminRole),
        new Claim(AppClaims.SecurityStamp, a.SecurityStamp ?? ""),
    ], AuthSchemes.Admin));

    public static int? CustomerId(this ClaimsPrincipal user) =>
        user.HasClaim(AppClaims.Role, AppClaims.CustomerRole) && int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static async Task ApiAwareRedirect(RedirectContext<CookieAuthenticationOptions> ctx, int status, string error)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new { error });
            return;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
    }

    /// <summary>Kill customer sessions after deactivation or an access-code reset.</summary>
    private static async Task ValidateCustomerAsync(CookieValidatePrincipalContext ctx)
    {
        var id = ctx.Principal?.CustomerId();
        var version = int.TryParse(ctx.Principal?.FindFirstValue(AppClaims.AccessCodeVersion), out var v) ? v : -1;
        var auth = ctx.HttpContext.RequestServices.GetRequiredService<CustomerAuthService>();
        if (id is null || !await auth.IsSessionValidAsync(id.Value, version, ctx.HttpContext.RequestAborted))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(AuthSchemes.Customer);
        }
    }

    /// <summary>
    /// Kill admin sessions when the account is gone or its security stamp changes (password change/reset).
    /// Lockout only blocks new sign-ins; it must not end live sessions, or anyone could sign the admin out with bad passwords.
    /// </summary>
    private static async Task ValidateAdminAsync(CookieValidatePrincipalContext ctx)
    {
        var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
        var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await users.FindByIdAsync(id);
        var stamp = ctx.Principal?.FindFirstValue(AppClaims.SecurityStamp);
        if (user is null || user.SecurityStamp != stamp)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(AuthSchemes.Admin);
        }
    }
}
