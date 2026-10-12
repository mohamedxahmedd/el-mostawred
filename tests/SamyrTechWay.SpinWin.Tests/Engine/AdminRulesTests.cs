using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;
using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Engine;

/// <summary>
/// Admin-side rules: allocation validation (spec §12 case 9), single Try Again, soft delete, duplicate phones, credits.
/// Runs on SQLite; <see cref="SqlServerAdminRulesTests"/> reruns on SQL Server.
/// </summary>
public class AdminRulesTests : IAsyncLifetime
{
    private TestHost _host = null!;
    protected virtual Task<TestHost> CreateHostAsync() => TestHost.SqliteAsync();
    public async ValueTask InitializeAsync() => _host = await CreateHostAsync();
    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<ServiceResult> SaveAllocationAsync(params AllocationRow[] rows) =>
        _host.RunAsync(sp => sp.GetRequiredService<WheelItemAdminService>().SaveAllocationAsync(rows, TestHost.Admin));

    [Fact]
    public async Task Allocation_must_total_exactly_100_percent()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        await _host.WithDbAsync(db => db.WheelItems.Where(w => w.IsArchived).ExecuteDeleteAsync()); // only the two live rows

        var under = await SaveAllocationAsync(new(ids["Pad"], true, 7000, 1), new(ids["Try Again"], true, 2500, 2));
        var over = await SaveAllocationAsync(new(ids["Pad"], true, 8000, 1), new(ids["Try Again"], true, 2500, 2));
        var ok = await SaveAllocationAsync(new(ids["Pad"], true, 6000, 1), new(ids["Try Again"], true, 4000, 2));

        Assert.False(under.Ok);
        Assert.False(over.Ok);
        Assert.True(ok.Ok, ok.Error);
        Assert.Equal(6000, (await _host.WithDbAsync(db => db.WheelItems.SingleAsync(w => w.Id == ids["Pad"]))).ProbabilityBasisPoints);
    }

    [Fact]
    public async Task Allocation_rejects_a_wheel_with_only_try_again_active()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        await _host.WithDbAsync(db => db.WheelItems.Where(w => w.IsArchived).ExecuteDeleteAsync());
        var result = await SaveAllocationAsync(new(ids["Pad"], false, 0, 1), new(ids["Try Again"], true, 10_000, 2));
        Assert.False(result.Ok);
    }

    private static WheelItemInput PadInput(int basisPoints, bool active = true) => new("Pad", null, null, null, WheelItemKind.Prize, 0,
        basisPoints, "mousepad", "ruby", null, 3, active, CelebrationTier.Standard, null, null);

    private Task<ServiceResult<int?>> UpdateAsync(int id, WheelItemInput input) => _host.RunAsync(async sp =>
    {
        var item = await sp.GetRequiredService<SamyrTechWay.SpinWin.Data.AppDbContext>().WheelItems.AsNoTracking().SingleAsync(w => w.Id == id);
        return await sp.GetRequiredService<WheelItemAdminService>().UpdateAsync(id, input, null, false, item.RowVersion, TestHost.Admin);
    });

    private Task<int> WeightAsync(int id) => _host.WithDbAsync(async db => (await db.WheelItems.SingleAsync(w => w.Id == id)).ProbabilityBasisPoints);

    [Fact]
    public async Task Editing_one_prize_weight_lets_try_again_absorb_the_difference()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));

        var result = await UpdateAsync(ids["Pad"], PadInput(5000));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(5000, result.Value);
        Assert.Equal(5000, await WeightAsync(ids["Pad"]));
        Assert.Equal(5000, await WeightAsync(ids["Try Again"]));
    }

    [Fact]
    public async Task Edit_is_refused_with_a_visible_error_when_try_again_cannot_absorb_the_difference()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));

        var result = await UpdateAsync(ids["Pad"], PadInput(10_000)); // Try Again would drop to 0%

        Assert.False(result.Ok);
        Assert.Equal(nameof(SamyrTechWay.SpinWin.Models.ViewModels.WheelItemForm.WeightPercent), result.Field); // shown next to the input
        Assert.Equal(7500, await WeightAsync(ids["Pad"]));
        Assert.Equal(2500, await WeightAsync(ids["Try Again"]));
    }

    [Fact]
    public async Task Edit_is_refused_when_try_again_changed_after_it_was_read()
    {
        var ids = await _host.SetWheelAsync(
            ("Pad", WheelItemKind.Prize, 5000, null), ("Mug", WheelItemKind.Prize, 2500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));

        // Another admin saves a change to Try Again (Mug 25% -> 20%, Try Again 25% -> 30%) after this edit took its snapshot
        // of the wheel but before it loads Try Again to adjust it.
        var interceptor = new BeforeReadingItem(ids["Try Again"], () => _host.WithDbAsync(async db =>
        {
            (await db.WheelItems.SingleAsync(w => w.Id == ids["Mug"])).ProbabilityBasisPoints = 2000;
            (await db.WheelItems.SingleAsync(w => w.Id == ids["Try Again"])).ProbabilityBasisPoints = 3000;
            return await db.SaveChangesAsync();
        }));
        var result = await _host.RunWithInterceptorsAsync([interceptor], async sp =>
        {
            var item = await sp.GetRequiredService<SamyrTechWay.SpinWin.Data.AppDbContext>().WheelItems.AsNoTracking().SingleAsync(w => w.Id == ids["Pad"]);
            return await sp.GetRequiredService<WheelItemAdminService>().UpdateAsync(ids["Pad"], PadInput(6000), null, false, item.RowVersion, TestHost.Admin);
        });

        Assert.True(interceptor.Fired);
        Assert.False(result.Ok);
        Assert.Contains("حاول مرة أخرى", result.Error);
        var total = await _host.WithDbAsync(db => db.WheelItems.Where(w => w.IsActive && !w.IsArchived).SumAsync(w => w.ProbabilityBasisPoints));
        Assert.Equal(10_000, total); // the wheel still adds up
    }

    /// <summary>Runs concurrent work just before the first query that loads one specific wheel item by id.</summary>
    private sealed class BeforeReadingItem(int itemId, Func<Task> concurrentWork) : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.Contains("WheelItems")
                && command.Parameters.Cast<System.Data.Common.DbParameter>().Any(p => Equals(p.Value, itemId)))
            {
                Fired = true;
                await concurrentWork();
            }
            return result;
        }
    }

    [Fact]
    public async Task Clearing_credits_still_succeeds_when_a_spin_lands_mid_way()
    {
        // Needs real row-level locking: SQLite serialises the whole file, so a concurrent writer here would deadlock.
        Assert.SkipUnless(_host.IsSqlServer, "Concurrency case runs in the SQL Server suite.");
        await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await _host.CreateCustomerAsync(credits: 3);
        _host.Random.Next = _ => 0; // the spin wins, using one credit

        var interceptor = new BeforeFirstCreditsUpdate(async () => Assert.True((await _host.SpinAsync(id)).Succeeded));
        var cleared = await _host.RunWithInterceptorsAsync([interceptor],
            sp => sp.GetRequiredService<CustomerAdminService>().ClearCreditsAsync(id, TestHost.Admin));

        Assert.True(interceptor.Fired);
        Assert.True(cleared.Ok, cleared.Error);
        Assert.Equal(2, cleared.Value); // re-read after the spin
        Assert.Equal(0, (await _host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(0, await _host.WithDbAsync(db => db.CreditLedger.Where(l => l.CustomerId == id).SumAsync(l => l.Delta)));
    }

    private sealed class BeforeFirstCreditsUpdate(Func<Task> concurrentWork) : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("SpinCredits"))
            {
                Fired = true;
                await concurrentWork();
            }
            return result;
        }
    }

    [Fact]
    public async Task Clearing_credits_for_an_unknown_customer_fails()
    {
        var result = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().ClearCreditsAsync(999_999, TestHost.Admin));
        Assert.False(result.Ok);
        Assert.Equal(CustomerAdminService.CustomerNotFoundMessage, result.Error);
    }

    [Fact]
    public async Task Edit_that_breaks_100_percent_is_refused_when_there_is_no_active_try_again()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 5000, null), ("Mug", WheelItemKind.Prize, 5000, null));

        var result = await UpdateAsync(ids["Pad"], PadInput(4000));

        Assert.False(result.Ok);
        Assert.Equal(5000, await WeightAsync(ids["Pad"]));
    }

    [Fact]
    public async Task Turning_off_the_last_real_prize_from_its_edit_page_is_refused()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));

        var result = await UpdateAsync(ids["Pad"], PadInput(7500, active: false));

        Assert.False(result.Ok);
        Assert.Equal(2500, await WeightAsync(ids["Try Again"]));
    }

    [Fact]
    public async Task Weight_validation_errors_target_the_form_field()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var result = await UpdateAsync(ids["Pad"], PadInput(0));
        Assert.False(result.Ok);
        Assert.Equal(WheelItemAdminService.WeightField, result.Field);
    }

    [Fact]
    public async Task Clearing_credits_sets_zero_with_a_ledger_entry_and_blocks_spinning()
    {
        await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await _host.CreateCustomerAsync(credits: 3);

        var cleared = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().ClearCreditsAsync(id, TestHost.Admin));

        Assert.True(cleared.Ok, cleared.Error);
        Assert.Equal(3, cleared.Value);
        Assert.Equal(0, (await _host.CustomerAsync(id)).SpinCredits);
        var last = await _host.WithDbAsync(db => db.CreditLedger.Where(l => l.CustomerId == id).OrderByDescending(l => l.Id).FirstAsync());
        Assert.Equal((-3, 0, CreditReason.AdminRevoke), (last.Delta, last.BalanceAfter, last.Reason));
        Assert.Equal(0, await _host.WithDbAsync(db => db.CreditLedger.Where(l => l.CustomerId == id).SumAsync(l => l.Delta)));
        Assert.Equal(1, await _host.WithDbAsync(db => db.AdminAuditLogs.CountAsync(a => a.Action == "credits.clear" && a.TargetId == id.ToString())));
        Assert.Equal(SpinFailure.NoCredits, (await _host.SpinAsync(id)).Failure);
    }

    [Fact]
    public async Task Clearing_credits_when_already_zero_changes_nothing()
    {
        var id = await _host.CreateCustomerAsync(credits: 1);
        await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().ClearCreditsAsync(id, TestHost.Admin));
        var ledgerBefore = await _host.WithDbAsync(db => db.CreditLedger.CountAsync(l => l.CustomerId == id));

        var again = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().ClearCreditsAsync(id, TestHost.Admin));

        Assert.True(again.Ok);
        Assert.Equal(0, again.Value);
        Assert.Equal(ledgerBefore, await _host.WithDbAsync(db => db.CreditLedger.CountAsync(l => l.CustomerId == id)));
    }

    private Task<ServiceResult> DeleteCustomerAsync(int id) =>
        _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().DeleteAsync(id, TestHost.Admin));

    [Fact]
    public async Task Deleting_a_customer_removes_them_and_their_history_and_frees_the_phone()
    {
        await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        const string phone = "01055556666";
        var (id, _) = await _host.CreateCustomerWithCodeAsync(phone, credits: 2);
        _host.Random.Next = _ => 9_999;
        Assert.True((await _host.SpinAsync(id)).Succeeded); // Try Again
        _host.Random.Next = _ => 0;
        var won = await _host.SpinAsync(id);
        Assert.Equal(DeliveryResult.Delivered,
            await _host.RunAsync(sp => sp.GetRequiredService<AwardService>().MarkDeliveredAsync(won.Award!.Id, null, TestHost.Admin)));

        var result = await DeleteCustomerAsync(id);

        Assert.True(result.Ok, result.Error);
        Assert.False(await _host.WithDbAsync(db => db.Customers.AnyAsync(c => c.Id == id)));
        Assert.Equal(0, await _host.WithDbAsync(db => db.SpinAttempts.CountAsync(a => a.CustomerId == id)));
        Assert.Equal(0, await _host.WithDbAsync(db => db.Awards.CountAsync(a => a.CustomerId == id)));
        Assert.Equal(0, await _host.WithDbAsync(db => db.CreditLedger.CountAsync(l => l.CustomerId == id)));
        Assert.Equal(1, await _host.WithDbAsync(db => db.AdminAuditLogs.CountAsync(a => a.Action == "customer.delete" && a.TargetId == id.ToString())));
        await _host.CreateCustomerWithCodeAsync(phone); // the number can be registered again
    }

    [Fact]
    public async Task Deleting_a_customer_is_refused_while_a_won_prize_waits_for_delivery()
    {
        await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await _host.CreateCustomerAsync(credits: 1);
        _host.Random.Next = _ => 0;
        Assert.True((await _host.SpinAsync(id)).Succeeded);

        var result = await DeleteCustomerAsync(id);

        Assert.False(result.Ok);
        Assert.Equal(CustomerAdminService.PendingAwardBlocksDeleteMessage, result.Error);
        Assert.True((await _host.CustomerAsync(id)).IsActive); // a refusal leaves the customer untouched
        Assert.Equal(1, await _host.WithDbAsync(db => db.Awards.CountAsync(a => a.CustomerId == id)));
    }

    [Fact]
    public async Task Deleting_an_unknown_customer_fails()
    {
        var result = await DeleteCustomerAsync(999_999);
        Assert.False(result.Ok);
        Assert.Equal(CustomerAdminService.CustomerNotFoundMessage, result.Error);
    }

    [Fact]
    public async Task Only_one_try_again_can_be_active()
    {
        await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var input = new WheelItemInput("Retry 2", null, null, null, WheelItemKind.TryAgain, 0, 0, "retry", "gold", null, 3, false,
            CelebrationTier.Standard, null, null);
        var created = await _host.RunAsync(sp => sp.GetRequiredService<WheelItemAdminService>().CreateAsync(input, null, TestHost.Admin));
        Assert.True(created.Ok); // inactive is fine

        // The database itself refuses a second active Try Again.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _host.WithDbAsync(async db =>
        {
            var second = await db.WheelItems.SingleAsync(w => w.Id == created.Value!.Id);
            second.IsActive = true;
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Active_items_cannot_be_archived_and_archiving_keeps_history()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var customer = await _host.CreateCustomerAsync();
        _host.Random.Next = _ => 0;
        await _host.SpinAsync(customer);

        var svc = (Func<IServiceProvider, WheelItemAdminService>)(sp => sp.GetRequiredService<WheelItemAdminService>());
        Assert.False((await _host.RunAsync(sp => svc(sp).ArchiveAsync(ids["Pad"], TestHost.Admin))).Ok);

        await _host.WithDbAsync(db => db.WheelItems.Where(w => w.Id == ids["Pad"]).ExecuteUpdateAsync(s => s.SetProperty(w => w.IsActive, false)));
        Assert.True((await _host.RunAsync(sp => svc(sp).ArchiveAsync(ids["Pad"], TestHost.Admin))).Ok);
        Assert.Equal(1, await _host.WithDbAsync(db => db.Awards.CountAsync(a => a.WheelItemId == ids["Pad"])));
        Assert.True((await _host.WithDbAsync(db => db.WheelItems.SingleAsync(w => w.Id == ids["Pad"]))).IsArchived);
    }

    [Fact]
    public async Task Duplicate_phone_in_any_format_is_a_friendly_error()
    {
        await _host.CreateCustomerAsync(phone: "01012345678");
        var result = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>()
            .CreateAsync(new CustomerInput("Other", "+20 101 234 5678", null, null, null), 1, TestHost.Admin));
        Assert.False(result.Ok);
        Assert.Equal(CustomerAdminService.DuplicatePhoneMessage, result.Error);
    }

    [Fact]
    public async Task Audit_entries_never_contain_access_codes_or_hashes()
    {
        var id = await _host.CreateCustomerAsync();
        var code = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().ResetAccessCodeAsync(id, TestHost.Admin));
        var hash = (await _host.CustomerAsync(id)).AccessCodeHash;
        var details = await _host.WithDbAsync(db => db.AdminAuditLogs.Select(a => a.Details ?? "").ToListAsync());
        Assert.All(details, d =>
        {
            Assert.DoesNotContain(code.Value!, d);
            Assert.DoesNotContain(hash, d);
        });
    }
}
