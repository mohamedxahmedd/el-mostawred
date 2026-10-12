using System.Globalization;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Models.ViewModels;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Arabic labels and Cairo-time formatting for the admin dashboard (Arabic-only, RTL).</summary>
public static class AdminText
{
    private static readonly CultureInfo Arabic = CreateArabic();

    /// <summary>Arabic month and day names with Latin digits, so times and IDs stay easy to read and copy.</summary>
    private static CultureInfo CreateArabic()
    {
        var c = (CultureInfo)CultureInfo.GetCultureInfo("ar-EG").Clone();
        c.DateTimeFormat.Calendar = new GregorianCalendar();
        c.NumberFormat.NativeDigits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        return CultureInfo.ReadOnly(c);
    }

    public static string Date(DateTime utc, string format = "d MMM yyyy، HH:mm") =>
        CairoTime.ToCairo(utc).ToString(format, Arabic);

    /// <summary>The Arabic name when the admin entered one, otherwise the English name.</summary>
    public static string Name(string en, string? ar) => string.IsNullOrWhiteSpace(ar) ? en : ar;

    /// <summary>"لفّة واحدة", "لفّتان", "3 لفّات", "11 لفّة": Arabic counted-noun agreement.</summary>
    public static string Spins(int n) => Math.Abs(n) switch
    {
        1 => "لفّة واحدة",
        2 => "لفّتان",
        >= 3 and <= 10 => $"{n} لفّات",
        _ => $"{n} لفّة",
    };

    public static string Number(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    public static string Tier(CelebrationTier tier) => tier switch
    {
        CelebrationTier.Standard => "عادية",
        CelebrationTier.Rare => "نادرة",
        CelebrationTier.Epic => "كبرى",
        _ => tier.ToString(),
    };

    public static string Kind(WheelItemKind kind) => kind switch
    {
        WheelItemKind.TryAgain => "حاول مرة أخرى",
        WheelItemKind.Lose => "حظ أوفر",
        _ => "جائزة",
    };

    public static string Result(SpinResultKind kind) => kind switch
    {
        SpinResultKind.Won => "فوز",
        SpinResultKind.Lost => "حظ أوفر",
        _ => "حاول مرة أخرى",
    };

    public static string Status(AwardStatus status) => status == AwardStatus.Delivered ? "تم التسليم" : "في انتظار التسليم";

    public static string State(CustomerState state) => state switch
    {
        CustomerState.Eligible => "له لفّات",
        CustomerState.Finished => "استنفد لفّاته",
        _ => "موقوف",
    };

    public static string Reason(CreditReason reason) => reason switch
    {
        CreditReason.Initial => "رصيد البداية",
        CreditReason.AdminGrant => "إضافة من الإدارة",
        CreditReason.AdminRevoke => "خصم من الإدارة",
        CreditReason.SpinDebit => "لفّة",
        CreditReason.TryAgainRefund => "استرداد (حاول مرة أخرى)",
        _ => reason.ToString(),
    };

    public static string Intensity(AnimationIntensity intensity) => intensity switch
    {
        AnimationIntensity.Subtle => "هادئة",
        AnimationIntensity.Standard => "عادية",
        AnimationIntensity.Cinematic => "قوية",
        _ => intensity.ToString(),
    };

    public static string Action(string action) => action switch
    {
        "admin.bootstrap-reset" => "استعادة حساب المدير",
        "admin.password-change" => "تغيير كلمة المرور",
        "award.deliver" => "تسليم جائزة",
        "credits.clear" => "تصفير اللفّات",
        "credits.grant" => "إضافة لفّات",
        "credits.revoke" => "خصم لفّات",
        "customer.activate" => "تفعيل عميل",
        "customer.create" => "إضافة عميل",
        "customer.delete" => "حذف عميل",
        "customer.deactivate" => "إيقاف عميل",
        "customer.reset-code" => "إصدار كود دخول جديد",
        "customer.update" => "تعديل عميل",
        "prize.allocation" => "تعديل النسب",
        "prize.archive" => "أرشفة جائزة",
        "prize.create" => "إضافة جائزة",
        "prize.restore" => "استعادة جائزة",
        "prize.update" => "تعديل جائزة",
        "settings.update" => "تعديل الإعدادات",
        _ => action,
    };

    public static string Target(string targetType) => targetType switch
    {
        "Customer" => "عميل",
        "WheelItem" => "جائزة",
        "Award" => "جائزة مكتسبة",
        "AdminUser" => "مدير",
        "SiteSettings" => "الإعدادات",
        _ => targetType,
    };
}
