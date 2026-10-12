using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public sealed record WheelItemDto(
    int Id,
    string Name,
    string Kind,
    string Tier,
    string? ImageUrl,
    string Icon,
    string Color,
    double Probability,
    string Percent,
    string? Description);

public sealed record PrizeDto(string Name, string Kind, string Tier, string? ImageUrl, string Icon, string Color);

/// <summary>Safe, customer-facing presentation of wheel items. Never includes stock, weights config or customer data.</summary>
public static class WheelPresenter
{
    public static string? ImageUrl(Guid? imageId) => imageId is { } id ? $"/media/prize/{id:N}" : null;

    public static string Icon(string? iconKey, WheelItemKind kind) =>
        PrizeIcons.IsValid(iconKey) ? iconKey! : kind == WheelItemKind.TryAgain ? "retry" : "gift";

    public static string KindKey(WheelItemKind kind) => kind == WheelItemKind.TryAgain ? "tryAgain" : "prize";

    public static IReadOnlyList<WheelItemDto> Items(IEnumerable<WheelItem> items, WheelOdds odds, Localizer t)
    {
        var byId = items.ToDictionary(i => i.Id);
        return Items(odds.Eligible.Select(e => (byId[e.Item.Id], e.Weight)).ToList(), t);
    }

    /// <summary>Items in wheel order with probabilities proportional to their weights within this set.</summary>
    public static IReadOnlyList<WheelItemDto> Items(IReadOnlyList<(WheelItem Item, int Weight)> set, Localizer t)
    {
        var total = set.Sum(s => s.Weight);
        return set.Select(s =>
        {
            var item = s.Item;
            var p = total > 0 ? (double)s.Weight / total : 0;
            var description = item.Kind == WheelItemKind.TryAgain
                ? t["play.legend.retry"]
                : t.Pick(item.DescriptionEn ?? "", item.DescriptionAr);
            return new WheelItemDto(
                item.Id,
                t.Pick(item.NameEn, item.NameAr),
                KindKey(item.Kind),
                item.CelebrationTier.ToString(),
                ImageUrl(item.ImageId),
                Icon(item.IconKey, item.Kind),
                item.ColorKey,
                p,
                FormatPercent(p),
                string.IsNullOrWhiteSpace(description) ? null : description);
        }).ToList();
    }

    public static PrizeDto Prize(SpinAttempt a, Localizer t) => new(
        t.Pick(a.ItemNameEn, a.ItemNameAr), KindKey(a.ItemKind), a.CelebrationTier.ToString(),
        ImageUrl(a.ImageId), Icon(a.IconKey, a.ItemKind), a.ColorKey);

    public static PrizeDto Prize(Award a, Localizer t) => new(
        t.Pick(a.PrizeNameEn, a.PrizeNameAr), "prize", a.CelebrationTier.ToString(),
        ImageUrl(a.ImageId), Icon(a.IconKey, WheelItemKind.Prize), a.ColorKey);

    public static string FormatPercent(double p)
    {
        var pct = p * 100;
        return (pct >= 10 ? pct.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                          : pct.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)) + "%";
    }
}
