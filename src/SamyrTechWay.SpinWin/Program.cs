using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Options;
using SamyrTechWay.SpinWin.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<SecurityOptions>(config.GetSection(SecurityOptions.Section));
builder.Services.Configure<AdminBootstrapOptions>(config.GetSection(AdminBootstrapOptions.Section));
builder.Services.Configure<DatabaseOptions>(config.GetSection(DatabaseOptions.Section));
var security = config.GetSection(SecurityOptions.Section).Get<SecurityOptions>() ?? new SecurityOptions();

// ---- Data -------------------------------------------------------------------------------------
var connectionString = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Locally: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<connection string>\" " +
        "--project src/SamyrTechWay.SpinWin. On MonsterASP: set the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString, sql =>
{
    sql.EnableRetryOnFailure(maxRetryCount: 4, maxRetryDelay: TimeSpan.FromSeconds(4), errorNumbersToAdd: null);
    sql.CommandTimeout(30);
}));

builder.Services.AddIdentityCore<AdminUser>(o =>
    {
        o.Password.RequiredLength = 12;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredUniqueChars = 5;
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<ArabicIdentityErrorDescriber>();

builder.Services.AddDataProtection()
    .SetApplicationName("SamyrTechWay.SpinWin")
    .PersistKeysToDbContext<AppDbContext>();

// ---- Web ----------------------------------------------------------------------------------------
builder.Services.AddAppAuthentication(security.RequireHttps);
builder.Services.AddAppRateLimiting(config);
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = ".stw.af";
    o.Cookie.SecurePolicy = security.RequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Only admin forms bind typed values, and the admin dashboard is Arabic.
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "هذا الحقل مطلوب.");
    m.SetMissingBindRequiredValueAccessor(_ => "هذا الحقل مطلوب.");
    m.SetMissingKeyOrValueAccessor(() => "هذا الحقل مطلوب.");
    m.SetMissingRequestBodyRequiredValueAccessor(() => "الطلب فارغ.");
    m.SetAttemptedValueIsInvalidAccessor((value, _) => $"القيمة «{value}» غير صحيحة.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"القيمة «{value}» غير صحيحة.");
    m.SetUnknownValueIsInvalidAccessor(_ => "القيمة غير صحيحة.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "القيمة غير صحيحة.");
    m.SetValueIsInvalidAccessor(value => $"القيمة «{value}» غير صحيحة.");
    m.SetValueMustBeANumberAccessor(_ => "يجب إدخال رقم.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "يجب إدخال رقم.");
});
builder.Services.AddHttpContextAccessor();
// Emit Arabic as real characters instead of numeric entities (still fully HTML-encoded for safety).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = ImageValidator.MaxBytes + 512 * 1024);

if (security.TrustForwardedHeaders)
{
    // Parsed at startup so a bad value fails fast instead of on every request.
    var knownProxies = (security.KnownProxies ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(ip => System.Net.IPAddress.TryParse(ip, out var addr) ? addr
            : throw new InvalidOperationException($"Security:KnownProxies contains an invalid IP address: '{ip}'."))
        .ToList();
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1; // only the entry appended by the nearest proxy, never client-supplied ones
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        knownProxies.ForEach(o.KnownProxies.Add); // none listed = accept from any sender (see SecurityOptions)
    });
}

if (security.RequireHttps)
{
    builder.Services.AddHsts(o =>
    {
        o.MaxAge = TimeSpan.FromDays(180);
        o.IncludeSubDomains = false;
    });
}

// ---- Application services -----------------------------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRandomSource, CryptoRandomSource>();
builder.Services.AddSingleton<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddSingleton<AccessCodes>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SettingsAdminService>();
builder.Services.AddScoped<Localizer>();
builder.Services.AddScoped<CustomerAuthService>();
builder.Services.AddScoped<CustomerAdminService>();
builder.Services.AddScoped<WheelItemAdminService>();
builder.Services.AddScoped<AwardService>();
builder.Services.AddScoped<SpinService>();
builder.Services.AddScoped<CustomerQueries>();
builder.Services.AddScoped<AdminQueries>();
builder.Services.AddScoped<DbInitializer>();

var app = builder.Build();

// ---- Startup tasks ------------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await scope.ServiceProvider.GetRequiredService<DbInitializer>().RunAsync();
    }
    catch (Exception ex)
    {
        // Keep the site up (it will show friendly errors) so the problem is visible in hosting logs.
        logger.LogCritical(ex, "Database initialisation failed. Check ConnectionStrings__DefaultConnection and migrations");
    }
}

// ---- Pipeline -----------------------------------------------------------------------------------
if (security.TrustForwardedHeaders) app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error/500");
}

if (security.RequireHttps)
{
    if (!app.Environment.IsDevelopment()) app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAppSecurityHeaders();
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    await Localizer.ResolveAsync(ctx, ctx.RequestServices.GetRequiredService<SettingsService>());
    await next();
});

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();

/// <summary>Exposed for WebApplicationFactory integration tests.</summary>
public partial class Program;
