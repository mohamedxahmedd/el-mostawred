using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SamyrTechWay.SpinWin.Services;
using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Engine;

/// <summary>Customer access-code lockout. Runs on SQLite; <see cref="SqlServerCustomerLoginTests"/> reruns on SQL Server.</summary>
public class CustomerLoginTests : IAsyncLifetime
{
    private TestHost _host = null!;
    protected virtual Task<TestHost> CreateHostAsync() => TestHost.SqliteAsync();
    public async ValueTask InitializeAsync() => _host = await CreateHostAsync();
    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Lockout_counts_failures_that_commit_concurrently()
    {
        const string phone = "01011112222";
        var (id, code) = await _host.CreateCustomerWithCodeAsync(phone);

        // This request read FailedLoginCount = 0; four other wrong guesses commit before it records its own failure.
        var interceptor = new BeforeFirstUpdate(() => _host.WithDbAsync(db => db.Customers.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FailedLoginCount, CustomerAuthService.MaxFailedAttempts - 1))));
        var wrong = await _host.RunWithInterceptorsAsync([interceptor],
            sp => sp.GetRequiredService<CustomerAuthService>().LoginAsync(phone, "ZZZZZZZZZZ"));

        Assert.True(interceptor.Fired);
        Assert.Equal(CustomerLoginStatus.Rejected, wrong.Status);
        var right = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAuthService>().LoginAsync(phone, code));
        Assert.Equal(CustomerLoginStatus.Rejected, right.Status); // the 5th failure locked the account
    }

    [Fact]
    public async Task Fewer_than_max_failures_do_not_lock()
    {
        const string phone = "01011113333";
        var (_, code) = await _host.CreateCustomerWithCodeAsync(phone);
        for (var i = 0; i < CustomerAuthService.MaxFailedAttempts - 1; i++)
            await _host.RunAsync(sp => sp.GetRequiredService<CustomerAuthService>().LoginAsync(phone, "ZZZZZZZZZZ"));

        var right = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAuthService>().LoginAsync(phone, code));
        Assert.Equal(CustomerLoginStatus.Success, right.Status);
    }

    private sealed class BeforeFirstUpdate(Func<Task> concurrentWork) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                Fired = true;
                await concurrentWork();
            }
            return result;
        }
    }
}
