namespace SamyrTechWay.SpinWin.Services;

/// <summary>Built-in prize icon keys. Path data lives in wwwroot/js/app/icons.js (generated together).</summary>
public static class PrizeIcons
{
    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        ("gift", "علبة هدية"),
        ("mouse", "ماوس"),
        ("mousepad", "ماوس باد"),
        ("headphones", "سماعات رأس"),
        ("headset", "سماعة جيمنج"),
        ("backpack", "شنطة ظهر"),
        ("keyboard", "كيبورد"),
        ("usb", "فلاشة USB"),
        ("cleaning", "طقم تنظيف"),
        ("laptop", "لابتوب"),
        ("stand", "حامل لابتوب"),
        ("speaker", "سماعة خارجية"),
        ("cable", "كابل"),
        ("charger", "شاحن / باور بنك"),
        ("gamepad", "يد تحكم للألعاب"),
        ("watch", "ساعة ذكية"),
        ("phone", "موبايل"),
        ("storage", "وحدة تخزين"),
        ("bag", "شنطة تسوق"),
        ("voucher", "قسيمة شراء"),
        ("discount", "خصم"),
        ("trophy", "كأس"),
        ("crown", "تاج"),
        ("gem", "جوهرة"),
        ("star", "نجمة"),
        ("medal", "ميدالية"),
        ("sparkles", "لمعان"),
        ("retry", "حاول مرة أخرى"),
    ];

    private static readonly HashSet<string> Keys = All.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string? key) => key is not null && Keys.Contains(key);
}
