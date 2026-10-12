using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Minimal projection of a wheel item needed for odds and drawing.</summary>
public sealed record WheelItemSnapshot(
    int Id,
    WheelItemKind Kind,
    int BasisPoints,
    int? StockQuantity,
    bool IsActive,
    bool IsArchived,
    int DisplayOrder)
{
    public bool IsLive => IsActive && !IsArchived;
    public bool InStock => Kind != WheelItemKind.Prize || StockQuantity is null || StockQuantity > 0;
}

public sealed record EffectiveOdd(WheelItemSnapshot Item, int Weight, int RangeStart)
{
    public int RangeEndExclusive => RangeStart + Weight;
}

public enum WheelStatus
{
    Ready,
    InvalidAllocation,
    NoEligiblePrize,
}

/// <summary>
/// Pure odds maths. Configured weights are basis points and must total exactly 10,000 across live items.
/// Effective odds renormalise the eligible items (live, weight > 0, in stock). Try Again and "better luck" (Lose)
/// are eligible only while at least one real prize is eligible.
/// </summary>
public sealed class WheelOdds
{
    public const int FullAllocation = 10_000;

    private WheelOdds(WheelStatus status, int configuredTotal, IReadOnlyList<EffectiveOdd> eligible)
    {
        Status = status;
        ConfiguredTotal = configuredTotal;
        Eligible = eligible;
        EffectiveTotal = eligible.Sum(e => e.Weight);
    }

    public WheelStatus Status { get; }
    public int ConfiguredTotal { get; }
    public int EffectiveTotal { get; }
    public IReadOnlyList<EffectiveOdd> Eligible { get; }
    public bool CanSpin => Status == WheelStatus.Ready;

    public static WheelOdds Compute(IEnumerable<WheelItemSnapshot> items, IReadOnlySet<int>? excluded = null)
    {
        var live = items.Where(i => i.IsLive).OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToList();
        var configuredTotal = live.Sum(i => i.BasisPoints);

        var candidates = live
            .Where(i => i.BasisPoints > 0 && i.InStock && (excluded is null || !excluded.Contains(i.Id)))
            .ToList();
        var anyRealPrize = candidates.Any(i => i.Kind == WheelItemKind.Prize);

        var eligible = new List<EffectiveOdd>();
        if (anyRealPrize)
        {
            var cursor = 0;
            foreach (var item in candidates)
            {
                eligible.Add(new EffectiveOdd(item, item.BasisPoints, cursor));
                cursor += item.BasisPoints;
            }
        }

        var status = configuredTotal != FullAllocation ? WheelStatus.InvalidAllocation
            : !anyRealPrize ? WheelStatus.NoEligiblePrize
            : WheelStatus.Ready;

        return new WheelOdds(status, configuredTotal, eligible);
    }

    /// <summary>Picks using cumulative ranges. <paramref name="nextInt"/> must return a uniform value in [0, maxExclusive).</summary>
    public EffectiveOdd Draw(Func<int, int> nextInt)
    {
        if (!CanSpin || EffectiveTotal <= 0)
            throw new InvalidOperationException("The wheel cannot be drawn in its current state.");

        var roll = nextInt(EffectiveTotal);
        if (roll < 0 || roll >= EffectiveTotal)
            throw new InvalidOperationException("Random source returned an out-of-range value.");

        foreach (var odd in Eligible)
            if (roll < odd.RangeEndExclusive) return odd;

        throw new InvalidOperationException("Cumulative range selection failed.");
    }

    public double ProbabilityOf(int itemId)
    {
        var odd = Eligible.FirstOrDefault(e => e.Item.Id == itemId);
        return odd is null || EffectiveTotal == 0 ? 0 : (double)odd.Weight / EffectiveTotal;
    }
}
