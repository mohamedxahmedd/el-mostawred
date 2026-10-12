using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Options;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Controllers;

public sealed class HomeController(
    CustomerAuthService auth,
    CustomerQueries customers,
    SettingsService settings,
    Localizer t,
    IOptions<SecurityOptions> security,
    ILogger<HomeController> logger) : Controller
{
    [HttpGet("/")]
    [HttpGet("/login")]
    public async Task<IActionResult> Index(string? returnUrl, string? e, CancellationToken ct)
    {
        string? signedIn = null;
        if (User.CustomerId() is { } id) signedIn = (await customers.FindAsync(id, ct))?.FirstName;
        var error = e == "rate" ? t["login.error.rate"] : null;
        return View("Index", await LandingAsync(new CustomerLoginForm { ReturnUrl = returnUrl }, error, signedIn, ct));
    }

    [HttpPost("/login")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> Login(CustomerLoginForm form, CancellationToken ct)
    {
        string? error = null;
        if (!PhoneNumber.TryNormalize(form.Phone, out _)) error = t["login.error.phone"];
        else if (AccessCodes.Normalize(form.AccessCode) is null) error = t["login.error.code"];

        if (error is null)
        {
            var result = await auth.LoginAsync(form.Phone, form.AccessCode, ct);
            switch (result.Status)
            {
                case CustomerLoginStatus.Success:
                    await HttpContext.SignOutAsync(AuthSchemes.Customer);
                    await HttpContext.SignInAsync(AuthSchemes.Customer, AuthSetup.CustomerPrincipal(result.Customer!),
                        new AuthenticationProperties { IsPersistent = true });
                    logger.LogInformation("Customer {CustomerId} signed in", result.Customer!.Id);
                    return LocalRedirect(SafeReturn(form.ReturnUrl));
                case CustomerLoginStatus.Inactive:
                    error = t["login.error.inactive"];
                    break;
                default:
                    error = t["login.error.rejected"];
                    break;
            }
        }

        form.AccessCode = ""; // never echo the code back
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("Index", await LandingAsync(form, error, null, ct));
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Customer);
        return Redirect("/");
    }

    [HttpPost("/lang")]
    public IActionResult Language(string lang, string? returnUrl)
    {
        if (Localizer.Supported.Contains(lang))
        {
            Response.Cookies.Append(Localizer.CookieName, lang, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                IsEssential = true,
            });
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    private async Task<LandingViewModel> LandingAsync(CustomerLoginForm form, string? error, string? signedIn, CancellationToken ct)
    {
        var site = await settings.GetAsync(ct);
        return new LandingViewModel
        {
            Form = form,
            Error = error,
            SignedInFirstName = signedIn,
            Subheading = t.Pick(site.PromoSubheadingEn, site.PromoSubheadingAr),
            Announcement = t.Pick(site.AnnouncementEn ?? "", site.AnnouncementAr) is { Length: > 0 } a ? a : null,
            InsecureConnection = !Request.IsHttps && !security.Value.RequireHttps,
        };
    }

    private string SafeReturn(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) && returnUrl!.StartsWith('/') && !returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : "/play";
}
