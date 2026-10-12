namespace SamyrTechWay.SpinWin.Services;

/// <summary>Built-in prize icon keys. Path data lives in wwwroot/js/app/icons.js (generated together).</summary>
public static class PrizeIcons
{
    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        ("gift", "Gift box"),
        ("mouse", "Mouse"),
        ("mousepad", "Mouse pad"),
        ("headphones", "Headphones"),
        ("headset", "Gaming headset"),
        ("backpack", "Backpack"),
        ("keyboard", "Keyboard"),
        ("usb", "USB drive"),
        ("cleaning", "Cleaning kit"),
        ("laptop", "Laptop"),
        ("stand", "Laptop stand"),
        ("speaker", "Speaker"),
        ("cable", "Cable"),
        ("charger", "Charger / power bank"),
        ("gamepad", "Game controller"),
        ("watch", "Smart watch"),
        ("phone", "Phone"),
        ("storage", "Storage drive"),
        ("bag", "Shopping bag"),
        ("voucher", "Voucher"),
        ("discount", "Discount"),
        ("trophy", "Trophy"),
        ("crown", "Crown"),
        ("gem", "Gem"),
        ("star", "Star"),
        ("medal", "Medal"),
        ("sparkles", "Sparkles"),
        ("retry", "Try again"),
    ];

    private static readonly HashSet<string> Keys = All.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string? key) => key is not null && Keys.Contains(key);
}
