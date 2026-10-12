using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

public sealed class AccountController(UserManager<AdminUser> users, TimeProvider clock, ILogger<AccountController> logger) : AdminControllerBase
{
    private const string GenericFailure = "البريد الإلكتروني أو كلمة المرور غير صحيحة، أو الحساب موقوف مؤقتًا. حاول مرة أخرى بعد قليل.";

    // Hash checked for unknown e-mails so they cost the same PBKDF2 work as real ones (no account enumeration by timing).
    private static string? _dummyHash;

    [AllowAnonymous]
    [HttpGet("/admin/login")]
    public IActionResult Login(string? returnUrl, string? e)
    {
        if (e == "rate") ModelState.AddModelError(string.Empty, "محاولات تسجيل دخول كثيرة. انتظر دقيقة وحاول مرة أخرى.");
        return View(new AdminLoginForm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("/admin/login")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> Login(AdminLoginForm form)
    {
        if (!ModelState.IsValid) return View(form);

        var user = await users.FindByEmailAsync(form.Email.Trim());
        if (user is null)
        {
            _dummyHash ??= users.PasswordHasher.HashPassword(new AdminUser(), Guid.NewGuid().ToString("N"));
            users.PasswordHasher.VerifyHashedPassword(new AdminUser(), _dummyHash, form.Password);
            ModelState.AddModelError(string.Empty, GenericFailure);
            form.Password = "";
            return View(form);
        }

        if (await users.IsLockedOutAsync(user))
        {
            logger.LogWarning("Admin sign-in refused for locked account {AdminId}", user.Id);
            ModelState.AddModelError(string.Empty, GenericFailure);
            form.Password = "";
            return View(form);
        }

        if (!await users.CheckPasswordAsync(user, form.Password))
        {
            await users.AccessFailedAsync(user);
            logger.LogWarning("Admin sign-in failed for {AdminId}", user.Id);
            ModelState.AddModelError(string.Empty, GenericFailure);
            form.Password = "";
            return View(form);
        }

        await users.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = clock.GetUtcNow().UtcDateTime;
        await users.UpdateAsync(user);

        await HttpContext.SignInAsync(AuthSchemes.Admin, AuthSetup.AdminPrincipal(user), new AuthenticationProperties { IsPersistent = false });
        logger.LogInformation("Admin {AdminId} signed in", user.Id);
        var target = Url.IsLocalUrl(form.ReturnUrl) && form.ReturnUrl!.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? form.ReturnUrl : "/admin";
        return LocalRedirect(target);
    }

    [HttpPost("/admin/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        return Redirect("/admin/login");
    }

    [HttpGet("/admin/account/password")]
    public IActionResult Password() => View(new ChangePasswordForm());

    [HttpPost("/admin/account/password")]
    public async Task<IActionResult> Password(ChangePasswordForm form, [FromServices] AuditService audit, [FromServices] Data.AppDbContext db)
    {
        if (!ModelState.IsValid) return View(form);
        // The password change and its audit row commit together.
        var (user, result) = await db.InTransactionAsync<(AdminUser? User, IdentityResult Result)>(async _ =>
        {
            var user = await users.FindByIdAsync(Actor.Id);
            if (user is null) return (null, IdentityResult.Failed());
            var result = await users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
            if (result.Succeeded)
            {
                audit.Record(Actor, "admin.password-change", "AdminUser", user.Id);
                await db.SaveChangesAsync();
            }
            return (user, result);
        });
        if (user is null) return Redirect("/admin/login");
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(new ChangePasswordForm());
        }

        // Security stamp changed: re-issue this session, other sessions are invalidated on their next request.
        await HttpContext.SignInAsync(AuthSchemes.Admin, AuthSetup.AdminPrincipal(user));
        Toast("تم تغيير كلمة المرور. تم تسجيل الخروج من الجلسات الأخرى.");
        return Redirect("/admin");
    }
}
