using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;
using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Engine;

/// <summary>
/// Parallel requests from separate "devices" (separate DI scopes = separate connections and transactions).
/// SQLite serialises writers with a database lock; the SQL Server variant exercises real row locks.
/// </summary>
public class ConcurrencyTests : IAsyncLifetime
{
    protected TestHost Host = null!;
    protected virtual Task<TestHost> CreateHostAsync() => TestHost.SqliteAsync();
    public async ValueTask InitializeAsync() => Host = await CreateHostAsync();
    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    [Fact]
    public async Task Parallel_spins_cannot_overdraw_credits()
    {
        await Host.SetWheelAsync(("Pad", WheelItemKind.Prize, 10_000, null));
        var id = await Host.CreateCustomerAsync(credits: 2);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => Host.SpinAsync(id))));

        Assert.Equal(2, outcomes.Count(o => o.Succeeded));
        Assert.All(outcomes.Where(o => !o.Succeeded), o => Assert.Equal(SpinFailure.NoCredits, o.Failure));
        Assert.Equal(0, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(2, await Host.WithDbAsync(db => db.Awards.CountAsync(a => a.CustomerId == id)));
    }

    [Fact]
    public async Task Parallel_replays_of_one_key_produce_exactly_one_attempt()
    {
        await Host.SetWheelAsync(("Pad", WheelItemKind.Prize, 10_000, null));
        var id = await Host.CreateCustomerAsync(credits: 3);
        var key = Guid.NewGuid();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Host.SpinAsync(id, key))));

        Assert.All(outcomes, o => Assert.True(o.Succeeded));
        Assert.Single(outcomes.Select(o => o.Attempt!.Id).Distinct());
        Assert.Equal(2, (await Host.CustomerAsync(id)).SpinCredits);
        Assert.Equal(1, await Host.WithDbAsync(db => db.SpinAttempts.CountAsync()));
    }

    [Fact]
    public async Task Concurrent_winners_cannot_oversell_the_last_unit()
    {
        var items = await Host.SetWheelAsync(("Headset", WheelItemKind.Prize, 10_000, 1));
        var customers = new List<int>();
        for (var i = 0; i < 8; i++) customers.Add(await Host.CreateCustomerAsync());

        var outcomes = await Task.WhenAll(customers.Select(c => Task.Run(() => Host.SpinAsync(c))));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        Assert.Equal(7, outcomes.Count(o => o.Failure == SpinFailure.WheelUnavailable));
        Assert.Equal(0, await Host.StockAsync(items["Headset"]));
        Assert.Equal(1, await Host.WithDbAsync(db => db.Awards.CountAsync()));
        // Losers were rolled back, never debited.
        var credits = await Host.WithDbAsync(db => db.Customers.SumAsync(c => c.SpinCredits));
        Assert.Equal(7, credits);
    }

    [Fact]
    public async Task Concurrent_mixed_stock_keeps_ledger_and_stock_consistent()
    {
        var items = await Host.SetWheelAsync(
            ("Stand", WheelItemKind.Prize, 4000, 3),
            ("Pad", WheelItemKind.Prize, 3000, null),
            ("Try Again", WheelItemKind.TryAgain, 3000, null));
        var customers = new List<int>();
        for (var i = 0; i < 10; i++) customers.Add(await Host.CreateCustomerAsync(credits: 2));

        await Task.WhenAll(customers.SelectMany(c => new[] { c, c, c }).Select(c => Task.Run(() => Host.SpinAsync(c))));

        var standWins = await Host.WithDbAsync(db => db.Awards.CountAsync(a => a.WheelItemId == items["Stand"]));
        Assert.True(standWins <= 3);
        Assert.Equal(3 - standWins, await Host.StockAsync(items["Stand"]));
        foreach (var c in customers)
        {
            var customer = await Host.CustomerAsync(c);
            var ledger = await Host.WithDbAsync(db => db.CreditLedger.Where(l => l.CustomerId == c).SumAsync(l => l.Delta));
            Assert.Equal(customer.SpinCredits, ledger);
            Assert.InRange(customer.SpinCredits, 0, 2);
        }
    }
}
