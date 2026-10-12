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
    string? Description);

public sealed record PrizeDto(string Name, string Kind, string Tier, string? ImageUrl, string Icon, string Color);

/// <summary>Safe, customer-facing presentation of wheel items. Never includes stock, odds or customer data.</summary>
public static class WheelPresenter
{
    public static string? ImageUrl(Guid? imageId) => imageId is { } id ? $"/media/prize/{id:N}" : null;

    public static string Icon(string? iconKey, WheelItemKind kind) =>
        PrizeIcons.IsValid(iconKey) ? iconKey! : kind switch { WheelItemKind.TryAgain => "retry", WheelItemKind.Lose => "smile", _ => "gift" };

    public static string KindKey(WheelItemKind kind) => kind switch
    {
        WheelItemKind.TryAgain => "tryAgain",
        WheelItemKind.Lose => "lose",
        _ => "prize",
    };

    public static IReadOnlyList<WheelItemDto> Items(IEnumerable<WheelItem> items, WheelOdds odds, Localizer t)
    {
        var byId = items.ToDictionary(i => i.Id);
        return Items(odds.Eligible.Select(e => byId[e.Item.Id]).ToList(), t);
    }

    /// <summary>Items in wheel order. Every slice is drawn the same size, so the odds stay on the server.</summary>
    public static IReadOnlyList<WheelItemDto> Items(IReadOnlyList<WheelItem> ordered, Localizer t) =>
        ordered.Select(item =>
        {
            var description = item.Kind switch
            {
                WheelItemKind.TryAgain => t["play.legend.retry"],
                WheelItemKind.Lose when string.IsNullOrWhiteSpace(item.DescriptionEn) && string.IsNullOrWhiteSpace(item.DescriptionAr) => t["play.legend.lose"],
                _ => t.Pick(item.DescriptionEn ?? "", item.DescriptionAr),
            };
            return new WheelItemDto(
                item.Id,
                t.Pick(item.NameEn, item.NameAr),
                KindKey(item.Kind),
                item.CelebrationTier.ToString(),
                ImageUrl(item.ImageId),
                Icon(item.IconKey, item.Kind),
                item.ColorKey,
                string.IsNullOrWhiteSpace(description) ? null : description);
        }).ToList();

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
