using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Tests.Unit;

public class WheelOddsTests
{
    private static WheelItemSnapshot Prize(int id, int bp, int? stock = null, bool active = true) =>
        new(id, WheelItemKind.Prize, bp, stock, active, false, id);

    private static WheelItemSnapshot Retry(int id, int bp) => new(id, WheelItemKind.TryAgain, bp, null, true, false, id);

    [Fact]
    public void Ready_when_live_weights_total_exactly_100_percent()
    {
        var odds = WheelOdds.Compute([Prize(1, 6000), Prize(2, 1500), Retry(3, 2500)]);
        Assert.Equal(WheelStatus.Ready, odds.Status);
        Assert.Equal(10_000, odds.EffectiveTotal);
        Assert.Equal(0.6, odds.ProbabilityOf(1), 6);
    }

    [Theory]
    [InlineData(9_999)]
    [InlineData(10_001)]
    [InlineData(0)]
    public void Invalid_allocation_cannot_spin(int firstWeight)
    {
        var odds = WheelOdds.Compute([Prize(1, firstWeight), Retry(2, 0)]);
        Assert.False(odds.CanSpin);
        Assert.Equal(WheelStatus.InvalidAllocation, odds.Status);
    }

    [Fact]
    public void Inactive_items_do_not_count_toward_the_total()
    {
        var odds = WheelOdds.Compute([Prize(1, 7500), Retry(2, 2500), Prize(3, 5000, active: false)]);
        Assert.True(odds.CanSpin);
        Assert.DoesNotContain(odds.Eligible, e => e.Item.Id == 3);
    }

    [Fact]
    public void Sold_out_prizes_are_excluded_and_effective_odds_renormalise()
    {
        var odds = WheelOdds.Compute([Prize(1, 5000, stock: 0), Prize(2, 2500), Retry(3, 2500)]);
        Assert.True(odds.CanSpin);
        Assert.Equal(5_000, odds.EffectiveTotal);
        Assert.Equal(0, odds.ProbabilityOf(1));
        Assert.Equal(0.5, odds.ProbabilityOf(2), 6);
        Assert.Equal(0.5, odds.ProbabilityOf(3), 6);
    }

    [Fact]
    public void Try_again_alone_is_not_eligible_when_every_real_prize_is_gone()
    {
        var odds = WheelOdds.Compute([Prize(1, 7500, stock: 0), Retry(2, 2500)]);
        Assert.False(odds.CanSpin);
        Assert.Equal(WheelStatus.NoEligiblePrize, odds.Status);
        Assert.Empty(odds.Eligible);
    }

    [Fact]
    public void Cumulative_ranges_map_every_roll_to_exactly_one_item()
    {
        var odds = WheelOdds.Compute([Prize(1, 100), Prize(2, 7400), Retry(3, 2500)]);
        var counts = new Dictionary<int, int>();
        for (var roll = 0; roll < odds.EffectiveTotal; roll++)
        {
            var r = roll;
            var id = odds.Draw(_ => r).Item.Id;
            counts[id] = counts.GetValueOrDefault(id) + 1;
        }
        Assert.Equal(100, counts[1]);
        Assert.Equal(7400, counts[2]);
        Assert.Equal(2500, counts[3]);
    }

    [Fact]
    public void Draw_rejects_out_of_range_random_values()
    {
        var odds = WheelOdds.Compute([Prize(1, 10_000)]);
        Assert.Throws<InvalidOperationException>(() => odds.Draw(_ => 10_000));
        Assert.Throws<InvalidOperationException>(() => odds.Draw(_ => -1));
    }

    [Fact]
    public void Excluded_items_are_skipped_on_redraw()
    {
        var odds = WheelOdds.Compute([Prize(1, 5000, stock: 1), Prize(2, 2500), Retry(3, 2500)], new HashSet<int> { 1 });
        Assert.True(odds.CanSpin);
        Assert.DoesNotContain(odds.Eligible, e => e.Item.Id == 1);
    }
}
