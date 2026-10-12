using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;
using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Engine;

/// <summary>Spin engine behaviour (spec §12 cases 2–9, 11). Runs against SQLite; <see cref="SqlServerSpinEngineTests"/> reruns on SQL Server.</summary>
public class SpinEngineTests : IAsyncLifetime
{
    protected TestHost Host = null!;

    protected virtual Task<TestHost> CreateHostAsync() => TestHost.SqliteAsync();

    public async ValueTask InitializeAsync() => Host = await CreateHostAsync();
    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private Task<Dictionary<string, int>> StandardWheelAsync(int? mugStock = null) => Host.SetWheelAsync(
        ("Mug", WheelItemKind.Prize, 7500, mugStock),
        ("Try Again", WheelItemKind.TryAgain, 2500, null));

    private void ForceRoll(int value) => Host.Random.Next = _ => value;
    private void ForceWin() => ForceRoll(0);          // first cumulative range = Mug
    private void ForceRetry() => ForceRoll(9_999);    // last range = Try Again (when all in stock)

    [Fact]
    public async Task Better_luck_slice_uses_the_credit_and_gives_no_prize_and_no_refund()
    {
        var ids = await Host.SetWheelAsync(
            ("Mug", WheelItemKind.Prize, 5000, null),
            ("Better luck", WheelItemKind.Lose, 2500, null),
            ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await Host.CreateCustomerAsync(credits: 2);
        ForceRoll(5_000); // second range = Better luck

        var outcome = await Host.SpinAsync(id);

        Assert.True(outcome.Succeeded, outcome.Failure.ToString());
        Assert.Equal(SpinResultKind.Lost, outcome.Attempt!.Kind);
        Assert.Equal(ids["Better luck"], outcome.Attempt.WheelItemId);
        Assert.Null(outcome.Award);
        Assert.Equal((2, 1), (outcome.Attempt.CreditsBefore, outcome.Attempt.CreditsAfter));
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(0, await Host.WithDbAsync(db => db.Awards.CountAsync(a => a.CustomerId == id)));
        var spinLedger = await Host.WithDbAsync(db => db.CreditLedger.Where(l => l.SpinAttemptId == outcome.Attempt.Id).ToListAsync());
        Assert.Equal(CreditReason.SpinDebit, Assert.Single(spinLedger).Reason);
    }

    [Fact]
    public async Task A_wheel_without_a_real_prize_blocks_spins_even_with_better_luck_slices()
    {
        await Host.SetWheelAsync(
            ("Mug", WheelItemKind.Prize, 5000, 0), // sold out
            ("Better luck", WheelItemKind.Lose, 2500, null),
            ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await Host.CreateCustomerAsync(credits: 1);

        var outcome = await Host.SpinAsync(id);

        Assert.Equal(SpinFailure.WheelUnavailable, outcome.Failure);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
    }

    [Fact]
    public async Task New_customer_gets_one_initial_credit_with_a_ledger_entry()
    {
        var id = await Host.CreateCustomerAsync();
        var customer = await Host.CustomerAsync(id);
        Assert.Equal(1, customer.SpinCredits);
        var ledger = await Host.WithDbAsync(db => db.CreditLedger.Where(l => l.CustomerId == id).ToListAsync());
        var entry = Assert.Single(ledger);
        Assert.Equal(CreditReason.Initial, entry.Reason);
        Assert.Equal(1, entry.Delta);
    }

    [Fact]
    public async Task Retry_racing_with_its_own_commit_returns_the_stored_result_not_no_credits()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync(credits: 1);
        var key = Guid.NewGuid();
        ForceWin();

        // The retry misses the stored attempt; then, just before it reads the customer row, the original request commits.
        var interceptor = new BeforeFirstCustomerRead(() => Host.SpinAsync(id, key));
        var retry = await Host.RunWithInterceptorsAsync([interceptor],
            sp => sp.GetRequiredService<SpinService>().SpinAsync(id, key));

        Assert.True(interceptor.Fired);
        Assert.True(retry.Succeeded, $"expected the stored result, got {retry.Failure}");
        Assert.True(retry.Replayed);
        Assert.Equal(SpinResultKind.Won, retry.Attempt!.Kind);
        Assert.Equal(1, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync(a => a.CustomerId == id)));
        Assert.Equal(0, (await Host.CustomerAsync(id)).SpinCredits);
    }

    private sealed class BeforeFirstCustomerRead(Func<Task> concurrentWork) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.Contains("Customers", StringComparison.Ordinal))
            {
                Fired = true;
                await concurrentWork();
            }
            return result;
        }
    }

    [Fact]
    public async Task Cannot_spin_with_zero_credits()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync(credits: 0);
        var outcome = await Host.SpinAsync(id);
        Assert.Equal(SpinFailure.NoCredits, outcome.Failure);
        Assert.Equal(0, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync()));
    }

    [Fact]
    public async Task Cannot_spin_when_disabled()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync(active: false);
        var outcome = await Host.SpinAsync(id);
        Assert.Equal(SpinFailure.CustomerInactive, outcome.Failure);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
    }

    [Fact]
    public async Task Real_win_debits_once_reserves_stock_once_and_creates_one_award_and_one_attempt()
    {
        var items = await StandardWheelAsync(mugStock: 5);
        var id = await Host.CreateCustomerAsync();
        ForceWin();

        var outcome = await Host.SpinAsync(id);

        Assert.True(outcome.Succeeded);
        Assert.Equal(SpinResultKind.Won, outcome.Attempt!.Kind);
        Assert.Equal(items["Mug"], outcome.Attempt.WheelItemId);
        Assert.Equal(1, outcome.Attempt.CreditsBefore);
        Assert.Equal(0, outcome.Attempt.CreditsAfter);
        Assert.Equal(0, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(4, await Host.StockAsync(items["Mug"]));
        Assert.Equal(1, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync()));
        var award = Assert.Single(await Host.WithDbAsync(db => db.Awards.ToListAsync()));
        Assert.Equal(AwardStatus.Pending, award.Status);
        Assert.Equal("Mug", award.PrizeNameEn);
        Assert.Equal(outcome.Attempt.Id, award.SpinAttemptId);
    }

    [Fact]
    public async Task Try_again_refunds_atomically_logs_attempt_and_creates_no_award()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        ForceRetry();

        var outcome = await Host.SpinAsync(id);

        Assert.True(outcome.Succeeded);
        Assert.Equal(SpinResultKind.TryAgain, outcome.Attempt!.Kind);
        Assert.Equal(1, outcome.Attempt.CreditsBefore);
        Assert.Equal(1, outcome.Attempt.CreditsAfter);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(0, await Host.WithDbAsync(db => db.Awards.CountAsync()));
        var ledger = await Host.WithDbAsync(db => db.CreditLedger.Where(l => l.SpinAttemptId == outcome.Attempt.Id).ToListAsync());
        Assert.Contains(ledger, l => l.Reason == CreditReason.SpinDebit && l.Delta == -1);
        Assert.Contains(ledger, l => l.Reason == CreditReason.TryAgainRefund && l.Delta == 1);
    }

    [Fact]
    public async Task Consecutive_try_agains_never_dead_end_and_a_real_prize_eventually_lands()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        ForceRetry();
        for (var i = 0; i < 6; i++)
        {
            var retry = await Host.SpinAsync(id); // fresh key each time
            Assert.Equal(SpinResultKind.TryAgain, retry.Attempt!.Kind);
            Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
        }

        ForceWin();
        var win = await Host.SpinAsync(id);
        Assert.Equal(SpinResultKind.Won, win.Attempt!.Kind);
        Assert.Equal(0, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(7, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync(a => a.CustomerId == id)));
        Assert.Equal(1, await Host.WithDbAsync(db => db.Awards.CountAsync(a => a.CustomerId == id)));
    }

    [Fact]
    public async Task Real_randomness_with_a_mostly_retry_wheel_still_terminates_in_a_prize()
    {
        await Host.SetWheelAsync(("Sticker", WheelItemKind.Prize, 1000, null), ("Try Again", WheelItemKind.TryAgain, 9000, null));
        var id = await Host.CreateCustomerAsync();
        SpinOutcome outcome;
        var spins = 0;
        do
        {
            outcome = await Host.SpinAsync(id);
            Assert.True(outcome.Succeeded);
            spins++;
        } while (outcome.Attempt!.Kind == SpinResultKind.TryAgain && spins < 500);
        Assert.Equal(SpinResultKind.Won, outcome.Attempt.Kind);
    }

    [Fact]
    public async Task Duplicate_request_id_returns_the_same_result_without_another_debit_award_or_refund()
    {
        await StandardWheelAsync(mugStock: 3);
        var id = await Host.CreateCustomerAsync(credits: 2);
        var key = Guid.NewGuid();
        ForceWin();

        var first = await Host.SpinAsync(id, key);
        var second = await Host.SpinAsync(id, key);
        var third = await Host.SpinAsync(id, key);

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.True(third.Replayed);
        Assert.Equal(first.Attempt!.Id, second.Attempt!.Id);
        Assert.Equal(first.Attempt.Id, third.Attempt!.Id);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(1, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync()));
        Assert.Equal(1, await Host.WithDbAsync(db => db.Awards.CountAsync()));
        Assert.Equal(2, await Host.WithDbAsync(db => db.CreditLedger.CountAsync(l => l.CustomerId == id))); // initial + one debit
    }

    [Fact]
    public async Task Duplicate_try_again_key_does_not_refund_twice()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        var key = Guid.NewGuid();
        ForceRetry();

        await Host.SpinAsync(id, key);
        var replay = await Host.SpinAsync(id, key);

        Assert.True(replay.Replayed);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(1, await Host.WithDbAsync(db => db.CreditLedger.CountAsync(l => l.Reason == CreditReason.TryAgainRefund)));
    }

    [Fact]
    public async Task Request_id_is_scoped_per_customer()
    {
        await StandardWheelAsync();
        var a = await Host.CreateCustomerAsync();
        var b = await Host.CreateCustomerAsync();
        var key = Guid.NewGuid();
        ForceWin();

        var first = await Host.SpinAsync(a, key);
        var other = await Host.SpinAsync(b, key);

        Assert.False(other.Replayed);
        Assert.NotEqual(first.Attempt!.Id, other.Attempt!.Id);
        Assert.Equal(b, other.Attempt.CustomerId);
    }

    [Fact]
    public async Task Empty_request_id_is_rejected()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        Assert.Equal(SpinFailure.InvalidRequest, (await Host.SpinAsync(id, Guid.Empty)).Failure);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
    }

    [Fact]
    public async Task Zero_eligible_real_prizes_blocks_the_spin_without_debiting()
    {
        await Host.SetWheelAsync(("Mug", WheelItemKind.Prize, 7500, 0), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await Host.CreateCustomerAsync();

        var outcome = await Host.SpinAsync(id);

        Assert.Equal(SpinFailure.WheelUnavailable, outcome.Failure);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(0, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync()));
        Assert.Equal(1, await Host.WithDbAsync(db => db.CreditLedger.CountAsync()));
    }

    [Fact]
    public async Task Invalid_allocation_blocks_the_spin_without_debiting()
    {
        await Host.SetWheelAsync(("Mug", WheelItemKind.Prize, 5000, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var id = await Host.CreateCustomerAsync();
        Assert.Equal(SpinFailure.WheelUnavailable, (await Host.SpinAsync(id)).Failure);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
    }

    [Fact]
    public async Task Paused_campaign_blocks_spins_without_debiting()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        await Host.RunAsync(sp => sp.GetRequiredService<SettingsAdminService>()
            .UpdateAsync(s => s.CampaignEnabled = false, null, TestHost.Admin));

        Assert.Equal(SpinFailure.CampaignPaused, (await Host.SpinAsync(id)).Failure);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
    }

    [Fact]
    public async Task Sold_out_prize_is_never_drawn_and_last_unit_is_reserved_once()
    {
        var items = await Host.SetWheelAsync(
            ("Headset", WheelItemKind.Prize, 5000, 1),
            ("Pad", WheelItemKind.Prize, 2500, null),
            ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var first = await Host.CreateCustomerAsync();
        var second = await Host.CreateCustomerAsync();
        ForceWin(); // roll 0 → first eligible range

        var a = await Host.SpinAsync(first);
        var b = await Host.SpinAsync(second);

        Assert.Equal(items["Headset"], a.Attempt!.WheelItemId);
        Assert.Equal(items["Pad"], b.Attempt!.WheelItemId); // headset now excluded, roll 0 maps to Pad
        Assert.Equal(0, await Host.StockAsync(items["Headset"]));
    }

    [Fact]
    public async Task Snapshot_is_kept_when_the_prize_is_later_renamed()
    {
        var items = await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        ForceWin();
        var outcome = await Host.SpinAsync(id);

        await Host.WithDbAsync(db => db.WheelItems.Where(w => w.Id == items["Mug"])
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.NameEn, "Renamed")));

        var award = await Host.WithDbAsync(db => db.Awards.SingleAsync());
        var attempt = await Host.WithDbAsync(db => db.SpinAttempts.SingleAsync());
        Assert.Equal("Mug", award.PrizeNameEn);
        Assert.Equal("Mug", attempt.ItemNameEn);
        Assert.Equal(7500, attempt.ConfiguredBasisPoints);
        Assert.Equal(outcome.Attempt!.Id, attempt.Id);
    }

    [Fact]
    public async Task Admin_grant_allows_a_new_spin_after_winning_and_preserves_history()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        ForceWin();
        await Host.SpinAsync(id);
        Assert.Equal(SpinFailure.NoCredits, (await Host.SpinAsync(id)).Failure);

        var grant = await Host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>()
            .AdjustCreditsAsync(id, 1, "Second laptop purchase INV-2", TestHost.Admin));
        Assert.True(grant.Ok);
        Assert.Equal(1, grant.Value);

        var again = await Host.SpinAsync(id);
        Assert.True(again.Succeeded);
        Assert.Equal(2, await Host.WithDbAsync(db => db.Awards.CountAsync(a => a.CustomerId == id)));

        var audit = await Host.WithDbAsync(db => db.AdminAuditLogs.Where(a => a.Action == "credits.grant").ToListAsync());
        var entry = Assert.Single(audit);
        Assert.Equal(TestHost.Admin.Id, entry.AdminUserId);
        Assert.Contains("Second laptop purchase", entry.Details);

        // Ledger reconciles with stored credits.
        var sum = await Host.WithDbAsync(db => db.CreditLedger.Where(l => l.CustomerId == id).SumAsync(l => l.Delta));
        Assert.Equal((await Host.CustomerAsync(id)).SpinCredits, sum);
    }

    [Fact]
    public async Task Credits_can_never_be_revoked_below_zero()
    {
        var id = await Host.CreateCustomerAsync(credits: 1);
        var result = await Host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>()
            .AdjustCreditsAsync(id, -2, "Correction", TestHost.Admin));
        Assert.False(result.Ok);
        Assert.Equal(1, (await Host.CustomerAsync(id)).SpinCredits);
    }

    [Fact]
    public async Task Credit_changes_require_a_reason()
    {
        var id = await Host.CreateCustomerAsync();
        var result = await Host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>()
            .AdjustCreditsAsync(id, 1, "  ", TestHost.Admin));
        Assert.False(result.Ok);
        Assert.Equal("Reason", result.Field);
    }

    [Fact]
    public async Task Award_delivery_is_idempotent_and_audited_once()
    {
        await StandardWheelAsync();
        var id = await Host.CreateCustomerAsync();
        ForceWin();
        await Host.SpinAsync(id);
        var awardId = await Host.WithDbAsync(db => db.Awards.Select(a => a.Id).SingleAsync());

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Host.RunAsync(sp => sp.GetRequiredService<AwardService>().MarkDeliveredAsync(awardId, "Handed over", TestHost.Admin))));

        Assert.Equal(1, results.Count(r => r == DeliveryResult.Delivered));
        Assert.Equal(3, results.Count(r => r == DeliveryResult.AlreadyDelivered));
        var award = await Host.WithDbAsync(db => db.Awards.SingleAsync());
        Assert.Equal(AwardStatus.Delivered, award.Status);
        Assert.Equal(TestHost.Admin.Name, award.DeliveredByAdminName);
        Assert.Equal(1, await Host.WithDbAsync(db => db.AdminAuditLogs.CountAsync(a => a.Action == "award.deliver")));
    }
}
