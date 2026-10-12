using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Tests.Support;

/// <summary>Controllable random source; defaults to the real CSPRNG.</summary>
public sealed class TestRandom : IRandomSource
{
    public Func<int, int>? Next { get; set; }
    public int NextInt(int maxExclusive) => Next?.Invoke(maxExclusive) ?? System.Security.Cryptography.RandomNumberGenerator.GetInt32(maxExclusive);
}

/// <summary>
/// Real application services over a real relational database. SQLite (file-backed, so separate connections
/// genuinely contend for locks) by default; SQL Server when <see cref="SqlServerAsync"/> is used.
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    public static readonly AdminActor Admin = new("admin-test-id", "Test Admin");

    private readonly Action<DbContextOptionsBuilder> _configure;
    private readonly ServiceProvider _services;
    private readonly Func<Task> _cleanup;

    private TestHost(Action<DbContextOptionsBuilder> configure, Func<Task> cleanup)
    {
        _configure = configure;
        _services = Build(configure);
        _cleanup = cleanup;
    }

    public TestRandom Random => _services.GetRequiredService<TestRandom>();

    public static async Task<TestHost> SqliteAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stw-test-{Guid.NewGuid():N}.db");
        var cs = new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 60, Pooling = false }.ToString();
        var host = new TestHost(o => o.UseSqlite(cs), () =>
        {
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
                if (File.Exists(f)) File.Delete(f);
            return Task.CompletedTask;
        });
        await host.WithDbAsync(db => db.Database.EnsureCreatedAsync());
        await host.SeedBaselineAsync();
        return host;
    }

    /// <summary>Creates a throwaway database on the given SQL Server and applies the real migrations.</summary>
    public static async Task<TestHost> SqlServerAsync(string serverConnectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = $"stw_test_{Guid.NewGuid():N}",
        };
        var cs = builder.ConnectionString;
        var dbName = builder.InitialCatalog;
        var master = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = "master" }.ConnectionString;
        var host = new TestHost(o => o.UseSqlServer(cs, s => s.EnableRetryOnFailure(3)), async () =>
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(master);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF DB_ID('{dbName}') IS NOT NULL BEGIN ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]; END";
            await cmd.ExecuteNonQueryAsync();
        });
        await host.WithDbAsync(db => db.Database.MigrateAsync().ContinueWith(_ => true));
        await host.SeedBaselineAsync();
        return host;
    }

    private static ServiceProvider Build(Action<DbContextOptionsBuilder> configure)
    {
        var s = new ServiceCollection();
        s.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        s.AddDbContext<AppDbContext>(configure);
        s.AddMemoryCache();
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton<TestRandom>();
        s.AddSingleton<IRandomSource>(sp => sp.GetRequiredService<TestRandom>());
        s.AddSingleton<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
        s.AddSingleton<AccessCodes>();
        s.AddScoped<SettingsService>();
        s.AddScoped<AuditService>();
        s.AddScoped<SpinService>();
        s.AddScoped<CustomerAuthService>();
        s.AddScoped<CustomerAdminService>();
        s.AddScoped<WheelItemAdminService>();
        s.AddScoped<AwardService>();
        s.AddScoped<SettingsAdminService>();
        s.AddScoped<AdminQueries>();
        return s.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Runs work in a fresh DI scope (a fresh DbContext/connection), like a separate HTTP request.</summary>
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    /// <summary>Like <see cref="RunAsync{T}"/>, but the DbContext runs the given interceptors (to inject races at exact points).</summary>
    public async Task<T> RunWithInterceptorsAsync<T>(IInterceptor[] interceptors, Func<IServiceProvider, Task<T>> work)
    {
        await using var services = Build(o => { _configure(o); o.AddInterceptors(interceptors); });
        await using var scope = services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work) =>
        RunAsync(sp => work(sp.GetRequiredService<AppDbContext>()));

    public Task<SpinOutcome> SpinAsync(int customerId, Guid? requestId = null) =>
        RunAsync(sp => sp.GetRequiredService<SpinService>().SpinAsync(customerId, requestId ?? Guid.NewGuid()));

    private async Task SeedBaselineAsync()
    {
        await WithDbAsync(async db =>
        {
            var settings = SettingsService.Defaults();
            settings.UpdatedAtUtc = DateTime.UtcNow;
            db.SiteSettings.Add(settings);
            return await db.SaveChangesAsync();
        });
    }

    public Task<int> CreateCustomerAsync(int credits = 1, bool active = true, string? phone = null) =>
        RunAsync(async sp =>
        {
            var svc = sp.GetRequiredService<CustomerAdminService>();
            phone ??= "010" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10_000_000, 99_999_999);
            var result = await svc.CreateAsync(new CustomerInput("Test Customer", phone, "INV-1", null, null), credits, Admin);
            Assert.True(result.Ok, result.Error);
            var id = result.Value!.Customer.Id;
            if (!active) await svc.SetActiveAsync(id, false, Admin);
            return id;
        });

    public Task<(int Id, string Code)> CreateCustomerWithCodeAsync(string phone, int credits = 1) =>
        RunAsync(async sp =>
        {
            var result = await sp.GetRequiredService<CustomerAdminService>()
                .CreateAsync(new CustomerInput("Mona Ahmed", phone, null, null, null), credits, Admin);
            Assert.True(result.Ok, result.Error);
            return (result.Value!.Customer.Id, result.Value.AccessCode);
        });

    /// <summary>Replaces the wheel with the given live items (weights in basis points).</summary>
    public Task<Dictionary<string, int>> SetWheelAsync(params (string Name, WheelItemKind Kind, int Bp, int? Stock)[] items) =>
        WithDbAsync(async db =>
        {
            await db.WheelItems.ExecuteUpdateAsync(s => s.SetProperty(w => w.IsActive, false).SetProperty(w => w.IsArchived, true));
            var now = DateTime.UtcNow;
            var entities = items.Select((i, n) => new WheelItem
            {
                NameEn = i.Name, Kind = i.Kind, ProbabilityBasisPoints = i.Bp, StockQuantity = i.Stock,
                IsActive = true, DisplayOrder = n, ColorKey = "ruby", IconKey = i.Kind == WheelItemKind.TryAgain ? "retry" : "gift",
                CelebrationTier = CelebrationTier.Standard, CreatedAtUtc = now, UpdatedAtUtc = now,
            }).ToList();
            db.WheelItems.AddRange(entities);
            await db.SaveChangesAsync();
            return entities.ToDictionary(e => e.NameEn, e => e.Id);
        });

    public Task<Customer> CustomerAsync(int id) =>
        WithDbAsync(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == id));

    public Task<int> StockAsync(int itemId) =>
        WithDbAsync(async db => (await db.WheelItems.AsNoTracking().SingleAsync(w => w.Id == itemId)).StockQuantity ?? -1);

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _cleanup();
    }
}
