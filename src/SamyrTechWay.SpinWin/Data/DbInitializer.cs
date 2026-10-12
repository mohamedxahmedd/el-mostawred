using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Options;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Data;

/// <summary>
/// Startup tasks: optional migrations, required singleton rows (settings + one Try Again item),
/// Development-only demo prizes, and admin bootstrap from configuration (never hard-coded).
/// </summary>
public sealed class DbInitializer(
    AppDbContext db,
    UserManager<AdminUser> users,
    IOptions<DatabaseOptions> dbOptions,
    IOptions<AdminBootstrapOptions> bootstrap,
    IHostEnvironment env,
    TimeProvider clock,
    ILogger<DbInitializer> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (db.Database.IsSqlServer())
        {
            if (dbOptions.Value.ApplyMigrationsOnStartup)
            {
                logger.LogWarning("Applying EF Core migrations at startup (Database:ApplyMigrationsOnStartup=true)");
                await db.Database.MigrateAsync(ct);
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                logger.LogCritical(
                    "Database is missing {Count} migration(s): {Migrations}. Apply them (see docs/MONSTERASP_DEPLOY.md) before using the site",
                    pending.Count, string.Join(", ", pending));
                return;
            }
        }

        await EnsureSettingsAsync(ct);
        await EnsureTryAgainItemAsync(ct);
        if (env.IsDevelopment() && dbOptions.Value.SeedDemoPrizes) await SeedDemoPrizesAsync(ct);
        await BootstrapAdminAsync();
    }

    private async Task EnsureSettingsAsync(CancellationToken ct)
    {
        if (await db.SiteSettings.AnyAsync(ct)) return;
        var settings = SettingsService.Defaults();
        settings.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.SiteSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created default site settings");
    }

    private async Task EnsureTryAgainItemAsync(CancellationToken ct)
    {
        if (await db.WheelItems.AnyAsync(i => i.Kind == WheelItemKind.TryAgain, ct)) return;
        var now = clock.GetUtcNow().UtcDateTime;
        db.WheelItems.Add(new WheelItem
        {
            NameEn = "Try Again",
            NameAr = "حاول تاني",
            DescriptionEn = "So close! Your spin comes straight back.",
            DescriptionAr = "قرّبت! لفّتك بترجعلك على طول.",
            Kind = WheelItemKind.TryAgain,
            DisplayOrder = 100,
            ProbabilityBasisPoints = 0,
            IconKey = "retry",
            ColorKey = "gold",
            IsActive = false,
            CelebrationTier = CelebrationTier.Standard,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created the (inactive) Try Again wheel item");
    }

    /// <summary>Editable sample catalogue for local testing only. Never runs outside Development.</summary>
    private async Task SeedDemoPrizesAsync(CancellationToken ct)
    {
        if (await db.WheelItems.AnyAsync(i => i.Kind == WheelItemKind.Prize, ct)) return;
        var now = clock.GetUtcNow().UtcDateTime;
        WheelItem Prize(string en, string ar, string icon, string color, int bp, int? stock, CelebrationTier tier, int order) => new()
        {
            NameEn = en, NameAr = ar, Kind = WheelItemKind.Prize, IconKey = icon, ColorKey = color,
            ProbabilityBasisPoints = bp, StockQuantity = stock, CelebrationTier = tier, DisplayOrder = order,
            IsActive = true, LowStockThreshold = 3, CreatedAtUtc = now, UpdatedAtUtc = now,
        };

        db.WheelItems.AddRange(
            Prize("Mouse Pad", "ماوس باد", "mousepad", "ruby", 2600, null, CelebrationTier.Standard, 10),
            Prize("Cleaning Kit", "طقم تنظيف", "cleaning", "flame", 2000, 50, CelebrationTier.Standard, 20),
            Prize("Laptop Stand", "ستاند لابتوب", "stand", "ember", 1000, 15, CelebrationTier.Rare, 30),
            Prize("Wireless Mouse", "ماوس وايرلس", "mouse", "amber", 1200, 20, CelebrationTier.Rare, 40),
            Prize("Laptop Backpack", "شنطة لابتوب", "backpack", "onyx", 500, 10, CelebrationTier.Rare, 50),
            Prize("Gaming Headset", "سماعة جيمنج", "headset", "ruby", 200, 3, CelebrationTier.Epic, 60));

        var retry = await db.WheelItems.FirstAsync(i => i.Kind == WheelItemKind.TryAgain, ct);
        retry.ProbabilityBasisPoints = 2500;
        retry.IsActive = true;
        retry.DisplayOrder = 35;
        retry.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded Development demo prizes");
    }

    private const string BootstrapResetAction = "admin.bootstrap-reset";

    private async Task BootstrapAdminAsync()
    {
        var o = bootstrap.Value;
        if (string.IsNullOrWhiteSpace(o.Email) || string.IsNullOrWhiteSpace(o.Password)) return;

        var email = o.Email.Trim();
        var existing = await users.FindByEmailAsync(email);
        if (existing is null)
        {
            var admin = new AdminUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = string.IsNullOrWhiteSpace(o.DisplayName) ? email.Split('@')[0] : o.DisplayName.Trim(),
                LockoutEnabled = true,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            };
            var result = await users.CreateAsync(admin, o.Password);
            if (result.Succeeded)
                logger.LogWarning("Bootstrap admin {Email} created. Remove AdminBootstrap__Password from the environment now", email);
            else
                logger.LogError("Bootstrap admin could not be created: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        if (!o.ResetPasswordIfExists)
        {
            logger.LogInformation("Bootstrap admin {Email} already exists; AdminBootstrap settings ignored. Remove them from configuration", email);
            return;
        }

        // One-shot recovery: the setting is often left in place, and the Free plan restarts the app often.
        // Don't reset again once the admin has signed in after a configured reset (they may have changed it since),
        // and don't churn the security stamp (signing everyone out) when the configured password already works.
        var lastReset = await db.AdminAuditLogs.AsNoTracking()
            .Where(a => a.Action == BootstrapResetAction && a.TargetId == existing.Id)
            .OrderByDescending(a => a.Id)
            .Select(a => (DateTime?)a.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (lastReset is not null && existing.LastLoginAtUtc > lastReset)
        {
            logger.LogWarning("AdminBootstrap:ResetPasswordIfExists ignored for {Email}: the earlier reset has been used to sign in. Remove AdminBootstrap__* settings now", email);
            return;
        }
        if (await users.CheckPasswordAsync(existing, o.Password))
        {
            if (await users.IsLockedOutAsync(existing)) await users.SetLockoutEndDateAsync(existing, null);
            await users.ResetAccessFailedCountAsync(existing);
            logger.LogWarning("Bootstrap admin {Email} already uses the configured password (lockout cleared). Remove AdminBootstrap__* settings now", email);
            return;
        }

        var validation = await ValidatePasswordAsync(existing, o.Password);
        var reset = validation.Succeeded ? await users.RemovePasswordAsync(existing) : validation;
        if (reset.Succeeded) reset = await users.AddPasswordAsync(existing, o.Password);
        if (reset.Succeeded)
        {
            await users.SetLockoutEndDateAsync(existing, null);
            await users.ResetAccessFailedCountAsync(existing);
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminName = "Configuration (AdminBootstrap)",
                Action = BootstrapResetAction,
                TargetType = "AdminUser",
                TargetId = existing.Id,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
            logger.LogWarning("Bootstrap admin {Email} password reset via configuration. Remove AdminBootstrap__* settings now", email);
        }
        else
        {
            logger.LogError("Bootstrap admin password reset failed: {Errors}", string.Join("; ", reset.Errors.Select(e => e.Description)));
        }
    }

    private async Task<IdentityResult> ValidatePasswordAsync(AdminUser user, string password)
    {
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded) return result;
        }
        return IdentityResult.Success;
    }
}
