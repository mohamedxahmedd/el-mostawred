using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;
using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Web;

/// <summary>The real HTTP pipeline (auth schemes, policies, antiforgery, routing) over a SQLite database.</summary>
public sealed class SpinWinFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"stw-web-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused;Database=unused");
        builder.UseSetting("Security:RequireHttps", "false");
        builder.UseSetting("RateLimiting:LoginPerMinute", "1000");
        builder.UseSetting("RateLimiting:SpinPerMinute", "1000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath};Pooling=False"));
        });
    }

    public async Task InitialiseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<DbInitializer>().RunAsync();
        // Simple valid wheel: one prize + Try Again.
        var now = DateTime.UtcNow;
        db.WheelItems.Add(new WheelItem
        {
            NameEn = "Mouse Pad", NameAr = "ماوس باد", Kind = WheelItemKind.Prize, ProbabilityBasisPoints = 7500,
            IsActive = true, IconKey = "mousepad", ColorKey = "ruby", CreatedAtUtc = now, UpdatedAtUtc = now,
        });
        var retry = await db.WheelItems.SingleAsync(w => w.Kind == WheelItemKind.TryAgain);
        retry.IsActive = true;
        retry.ProbabilityBasisPoints = 2500;
        await db.SaveChangesAsync();
    }

    public async Task<(int Id, string Phone, string Code)> CreateCustomerAsync(int credits = 1)
    {
        await using var scope = Services.CreateAsyncScope();
        var phone = "010" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10_000_000, 99_999_999);
        var result = await scope.ServiceProvider.GetRequiredService<CustomerAdminService>()
            .CreateAsync(new CustomerInput("Mona Ahmed", phone, null, null, null), credits, TestHost.Admin);
        return (result.Value!.Customer.Id, phone, result.Value.AccessCode);
    }

    public async Task<T> WithServicesAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

public class WebTests : IAsyncLifetime
{
    private SpinWinFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new SpinWinFactory();
        await _factory.InitialiseAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string FormToken(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static string MetaToken(string html) =>
        Regex.Match(html, "name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string getPath, string postPath, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(getPath, Ct);
        fields["__RequestVerificationToken"] = FormToken(page);
        return await client.PostAsync(postPath, new FormUrlEncodedContent(fields), Ct);
    }

    private async Task<HttpClient> SignedInCustomerAsync(string phone, string code)
    {
        var client = NewClient();
        var res = await PostFormAsync(client, "/login", "/login", new() { ["Phone"] = phone, ["AccessCode"] = code });
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/play", res.Headers.Location!.OriginalString);
        return client;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> SpinAsync(HttpClient client, Guid requestId)
    {
        var csrf = MetaToken(await client.GetStringAsync("/play", Ct));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/spins") { Content = JsonContent.Create(new { requestId }) };
        req.Headers.Add("X-CSRF-TOKEN", csrf);
        var res = await client.SendAsync(req, Ct);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (res.StatusCode, body);
    }

    [Fact]
    public async Task Anonymous_users_cannot_use_the_api()
    {
        var client = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/wheel/config", Ct)).StatusCode);
        var spin = await client.PostAsJsonAsync("/api/spins", new { requestId = Guid.NewGuid() }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, spin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/spins/{Guid.NewGuid()}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_users_are_sent_to_the_right_login_pages()
    {
        var client = NewClient();
        var play = await client.GetAsync("/play", Ct);
        Assert.Equal(HttpStatusCode.Redirect, play.StatusCode);
        Assert.StartsWith("http://localhost/login", play.Headers.Location!.ToString());
        var admin = await client.GetAsync("/admin/customers", Ct);
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("/admin/login", admin.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Signed_in_customer_cannot_reach_admin()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);
        foreach (var path in new[] { "/admin", "/admin/customers", "/admin/prizes", "/admin/spins" })
        {
            var res = await client.GetAsync(path, Ct);
            Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("/admin/login", res.Headers.Location?.ToString() ?? "");
        }
    }

    [Fact]
    public async Task Spin_requires_the_antiforgery_header()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);
        var res = await client.PostAsJsonAsync("/api/spins", new { requestId = Guid.NewGuid() }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var credits = await _factory.WithServicesAsync(sp => sp.GetRequiredService<AppDbContext>().Customers.Where(x => x.Id == c.Id).Select(x => x.SpinCredits).SingleAsync());
        Assert.Equal(1, credits);
    }

    [Fact]
    public async Task Spin_endpoint_returns_201_then_200_for_the_same_key_and_409_when_out_of_credits()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);

        JsonElement first = default;
        HttpStatusCode status;
        Guid key;
        do
        {
            key = Guid.NewGuid();
            (status, first) = await SpinAsync(client, key);
            Assert.Equal(HttpStatusCode.Created, status);
        } while (first.GetProperty("result").GetString() == "tryAgain");

        var (replayStatus, replay) = await SpinAsync(client, key);
        Assert.Equal(HttpStatusCode.OK, replayStatus);
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(first.GetProperty("attemptId").GetString(), replay.GetProperty("attemptId").GetString());

        var (noCredit, body) = await SpinAsync(client, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, noCredit);
        Assert.Equal("noCredits", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Customers_cannot_see_each_others_results_or_prizes()
    {
        var a = await _factory.CreateCustomerAsync();
        var b = await _factory.CreateCustomerAsync();
        await _factory.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await db.Customers.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Bassem Private"));
            return 0;
        });
        var clientA = await SignedInCustomerAsync(a.Phone, a.Code);
        var clientB = await SignedInCustomerAsync(b.Phone, b.Code);

        var keyB = Guid.NewGuid();
        var (status, _) = await SpinAsync(clientB, keyB);
        Assert.Equal(HttpStatusCode.Created, status);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/spins/{keyB}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clientB.GetAsync($"/api/spins/{keyB}", Ct)).StatusCode);

        var prizesA = await clientA.GetStringAsync("/my-prizes", Ct);
        Assert.DoesNotContain("Bassem", prizesA);
        Assert.DoesNotContain(b.Phone[1..], prizesA);

        var config = await clientA.GetStringAsync("/api/wheel/config", Ct);
        Assert.DoesNotContain(a.Phone[1..], config);
        Assert.DoesNotContain("stock", config, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customers_never_receive_the_odds()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);

        var config = await client.GetStringAsync("/api/wheel/config", Ct);
        var (_, spin) = await SpinAsync(client, Guid.NewGuid());
        var play = await client.GetStringAsync("/play", Ct);

        foreach (var body in new[] { config, spin.GetRawText() })
        {
            Assert.DoesNotContain("probability", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("percent", body, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("legend-pct", play);
        Assert.Contains("\"name\"", config); // the prize list itself is still sent
    }

    [Fact]
    public async Task Phone_alone_never_signs_in_and_errors_do_not_reveal_registered_numbers()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = NewClient();

        var wrongCode = await PostFormAsync(client, "/login", "/login", new() { ["Phone"] = c.Phone, ["AccessCode"] = "ABCDE-23456" });
        var unknownPhone = await PostFormAsync(client, "/login", "/login", new() { ["Phone"] = "01599999999", ["AccessCode"] = "ABCDE-23456" });
        var emptyCode = await PostFormAsync(client, "/login", "/login", new() { ["Phone"] = c.Phone, ["AccessCode"] = "" });

        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPhone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyCode.StatusCode);
        string Alert(string html) => Regex.Match(html, "id=\"login-error\"[^>]*>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value;
        var wrongCodeAlert = Alert(await wrongCode.Content.ReadAsStringAsync(Ct));
        Assert.Contains("<span>", wrongCodeAlert);
        Assert.Equal(wrongCodeAlert, Alert(await unknownPhone.Content.ReadAsStringAsync(Ct)));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/play", Ct)).StatusCode);
    }

    [Fact]
    public async Task Repeated_wrong_codes_lock_the_account_even_for_the_right_code()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = NewClient();
        for (var i = 0; i < CustomerAuthService.MaxFailedAttempts; i++)
            await PostFormAsync(client, "/login", "/login", new() { ["Phone"] = c.Phone, ["AccessCode"] = "ABCDE-23456" });

        var right = await PostFormAsync(client, "/login", "/login", new() { ["Phone"] = c.Phone, ["AccessCode"] = c.Code });
        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);
    }

    [Fact]
    public async Task Resetting_the_code_ends_existing_sessions_and_old_code_stops_working()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play", Ct)).StatusCode);

        var newCode = await _factory.WithServicesAsync(sp => sp.GetRequiredService<CustomerAdminService>().ResetAccessCodeAsync(c.Id, TestHost.Admin));

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/play", Ct)).StatusCode);
        var old = await PostFormAsync(NewClient(), "/login", "/login", new() { ["Phone"] = c.Phone, ["AccessCode"] = c.Code });
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        await SignedInCustomerAsync(c.Phone, newCode.Value!);
    }

    [Fact]
    public async Task Disabled_customer_is_signed_out_and_cannot_sign_in()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);
        await _factory.WithServicesAsync(sp => sp.GetRequiredService<CustomerAdminService>().SetActiveAsync(c.Id, false, TestHost.Admin));

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/play", Ct)).StatusCode);
        var again = await PostFormAsync(NewClient(), "/login", "/login", new() { ["Phone"] = c.Phone, ["AccessCode"] = c.Code });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Re_enabling_a_customer_does_not_revive_sessions_ended_by_disabling()
    {
        var c = await _factory.CreateCustomerAsync();
        var client = await SignedInCustomerAsync(c.Phone, c.Code);
        await _factory.WithServicesAsync(sp => sp.GetRequiredService<CustomerAdminService>().SetActiveAsync(c.Id, false, TestHost.Admin));
        await _factory.WithServicesAsync(sp => sp.GetRequiredService<CustomerAdminService>().SetActiveAsync(c.Id, true, TestHost.Admin));

        // The old cookie was never presented while disabled; it must still be dead.
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/play", Ct)).StatusCode);
        await SignedInCustomerAsync(c.Phone, c.Code); // the code itself still works after re-enabling
    }

    private async Task<(string Email, string Password)> CreateAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@test.local";
        var password = "Aa1" + Guid.NewGuid().ToString("N");
        await _factory.WithServicesAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AdminUser>>();
            var r = await users.CreateAsync(new AdminUser { UserName = email, Email = email, DisplayName = "Tester", LockoutEnabled = true }, password);
            Assert.True(r.Succeeded);
            return 0;
        });
        return (email, password);
    }

    private async Task<HttpClient> SignedInAdminAsync(string email, string password)
    {
        var client = NewClient();
        var login = await PostFormAsync(client, "/admin/login", "/admin/login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task Leftover_reset_setting_does_not_undo_a_password_changed_after_recovery()
    {
        var (email, original) = await CreateAdminAsync();
        var recovery = "Rr9" + Guid.NewGuid().ToString("N");
        var chosenLater = "Cc7" + Guid.NewGuid().ToString("N");

        // An app start with AdminBootstrap__* still set, like a MonsterASP recycle.
        async Task RestartWithResetSettingAsync()
        {
            await using var restarted = _factory.WithWebHostBuilder(b => b
                .UseSetting("AdminBootstrap:Email", email)
                .UseSetting("AdminBootstrap:Password", recovery)
                .UseSetting("AdminBootstrap:ResetPasswordIfExists", "true"));
            _ = restarted.Services; // starts the host, which runs DbInitializer
        }

        await RestartWithResetSettingAsync();
        await SignedInAdminAsync(email, recovery); // recovery worked
        await _factory.WithServicesAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<AdminUser>>();
            var r = await users.ChangePasswordAsync((await users.FindByEmailAsync(email))!, recovery, chosenLater);
            Assert.True(r.Succeeded);
            return 0;
        });

        await RestartWithResetSettingAsync();
        await SignedInAdminAsync(email, chosenLater); // not reverted to the configured password
        Assert.NotEqual(original, chosenLater);
    }

    [Fact]
    public async Task Wrong_password_flood_locks_new_sign_ins_but_does_not_kick_out_a_signed_in_admin()
    {
        var (email, password) = await CreateAdminAsync();
        var admin = await SignedInAdminAsync(email, password);

        for (var i = 0; i < 5; i++)
            await PostFormAsync(NewClient(), "/admin/login", "/admin/login", new() { ["Email"] = email, ["Password"] = "wrong-password-123" });

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/admin/customers", Ct)).StatusCode);
        var fresh = await PostFormAsync(NewClient(), "/admin/login", "/admin/login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode); // locked: the login form is shown again, no redirect
    }

    [Fact]
    public async Task Admin_can_sign_in_and_admin_cookie_does_not_grant_customer_api()
    {
        var (email, password) = await CreateAdminAsync();

        var client = NewClient();
        var login = await PostFormAsync(client, "/admin/login", "/admin/login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/prizes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/wheel/config", Ct)).StatusCode);

        var bad = await PostFormAsync(NewClient(), "/admin/login", "/admin/login", new() { ["Email"] = email, ["Password"] = "wrong-password-123" });
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Invalid email or password", await bad.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Admin_password_change_is_audited_and_signs_out_other_sessions()
    {
        var (email, password) = await CreateAdminAsync();
        var here = await SignedInAdminAsync(email, password);
        var elsewhere = await SignedInAdminAsync(email, password);
        var next = "Nn5" + Guid.NewGuid().ToString("N");

        var res = await PostFormAsync(here, "/admin/account/password", "/admin/account/password",
            new() { ["CurrentPassword"] = password, ["NewPassword"] = next, ["ConfirmPassword"] = next });
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/admin", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await elsewhere.GetAsync("/admin", Ct)).StatusCode);
        var old = await PostFormAsync(NewClient(), "/admin/login", "/admin/login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        await SignedInAdminAsync(email, next);
        var audited = await _factory.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var id = (await sp.GetRequiredService<UserManager<AdminUser>>().FindByEmailAsync(email))!.Id;
            return await db.AdminAuditLogs.CountAsync(a => a.Action == "admin.password-change" && a.TargetId == id);
        });
        Assert.Equal(1, audited);
    }

    [Fact]
    public async Task Expired_admin_form_shows_a_plain_error_page_without_the_admin_shell()
    {
        var res = await NewClient().PostAsync("/admin/login",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "a@b.c", ["Password"] = "x" }), Ct); // no antiforgery token
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Form expired", html);
        Assert.DoesNotContain("class=\"sidebar\"", html);
    }

    /// <summary>Collects a page's form fields the way a browser would submit them (inputs, checked boxes/radios, selects, textareas).</summary>
    private static Dictionary<string, string> FormFields(string html, string formAction)
    {
        var form = Regex.Match(html, $"<form[^>]*action=\"{Regex.Escape(formAction)}\"[^>]*>(.*?)</form>", RegexOptions.Singleline).Groups[1].Value;
        var fields = new Dictionary<string, string>();
        string Attr(string tag, string name) => System.Net.WebUtility.HtmlDecode(Regex.Match(tag, $"\\s{name}=\"([^\"]*)\"").Groups[1].Value);
        foreach (Match m in Regex.Matches(form, "<input[^>]*>"))
        {
            var tag = m.Value; var name = Attr(tag, "name"); var type = Attr(tag, "type");
            if (name.Length == 0 || type == "file" || fields.ContainsKey(name)) continue;
            if (type is "checkbox" or "radio" && !tag.Contains("checked")) continue;
            fields[name] = Attr(tag, "value");
        }
        foreach (Match m in Regex.Matches(form, "<select[^>]*name=\"([^\"]+)\"[^>]*>(.*?)</select>", RegexOptions.Singleline))
            fields[m.Groups[1].Value] = Regex.Match(m.Groups[2].Value, "<option[^>]*value=\"([^\"]*)\"[^>]*selected").Groups[1].Value;
        foreach (Match m in Regex.Matches(form, "<textarea[^>]*name=\"([^\"]+)\"[^>]*>(.*?)</textarea>", RegexOptions.Singleline))
            fields[m.Groups[1].Value] = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value.Trim());
        return fields;
    }

    [Fact]
    public async Task Editing_a_prize_weight_rebalances_try_again_and_a_refused_edit_shows_its_error()
    {
        var (email, password) = await CreateAdminAsync();
        var admin = await SignedInAdminAsync(email, password);
        var (padId, retryId) = await _factory.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (await db.WheelItems.Where(w => w.NameEn == "Mouse Pad").Select(w => w.Id).SingleAsync(),
                    await db.WheelItems.Where(w => w.Kind == WheelItemKind.TryAgain).Select(w => w.Id).SingleAsync());
        });
        var editUrl = $"/admin/prizes/{padId}/edit";

        var fields = FormFields(await admin.GetStringAsync(editUrl, Ct), editUrl);
        fields["WeightPercent"] = "60";
        var saved = await admin.PostAsync(editUrl, new FormUrlEncodedContent(fields), Ct);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var weights = await _factory.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (await db.WheelItems.Where(w => w.Id == padId).Select(w => w.ProbabilityBasisPoints).SingleAsync(),
                    await db.WheelItems.Where(w => w.Id == retryId).Select(w => w.ProbabilityBasisPoints).SingleAsync());
        });
        Assert.Equal((6000, 4000), weights);

        fields = FormFields(await admin.GetStringAsync(editUrl, Ct), editUrl);
        fields["WeightPercent"] = "100"; // Try Again would have to drop to 0%
        var refused = await admin.PostAsync(editUrl, new FormUrlEncodedContent(fields), Ct);
        var html = await refused.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("can&#x27;t absorb that difference", html);
    }

    [Fact]
    public async Task Admin_can_set_a_customers_spins_to_zero()
    {
        var c = await _factory.CreateCustomerAsync(credits: 3);
        var (email, password) = await CreateAdminAsync();
        var admin = await SignedInAdminAsync(email, password);

        var res = await PostFormAsync(admin, $"/admin/customers/{c.Id}", $"/admin/customers/{c.Id}/credits/clear", new());

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var credits = await _factory.WithServicesAsync(sp => sp.GetRequiredService<AppDbContext>().Customers.Where(x => x.Id == c.Id).Select(x => x.SpinCredits).SingleAsync());
        Assert.Equal(0, credits);
        var customer = await SignedInCustomerAsync(c.Phone, c.Code);
        var (status, body) = await SpinAsync(customer, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("noCredits", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task There_is_no_public_registration_route()
    {
        var client = NewClient();
        foreach (var path in new[] { "/register", "/admin/register", "/signup", "/Identity/Account/Register" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, Ct)).StatusCode);
    }

    [Fact]
    public async Task Plain_http_without_require_https_warns_on_customer_and_admin_pages()
    {
        var client = NewClient();
        Assert.Contains("class=\"insecure-banner\"", await client.GetStringAsync("/", Ct));
        var adminLogin = await client.GetStringAsync("/admin/login", Ct);
        Assert.Contains("class=\"insecure-banner\"", adminLogin);
        Assert.Contains("Do not enter real customer codes", adminLogin);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var res = await NewClient().GetAsync("/", Ct);
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
