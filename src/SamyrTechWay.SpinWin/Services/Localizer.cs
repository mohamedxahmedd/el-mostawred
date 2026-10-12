using System.Globalization;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>
/// Lightweight two-language text table for the customer site (Arabic RTL default, English LTR).
/// Admin UI is English-only. Keys missing from the table render as the key itself so gaps are visible.
/// </summary>
public sealed class Localizer
{
    public const string CookieName = "stw.lang";
    public static readonly string[] Supported = ["ar", "en"];

    public const string ItemKey = "stw.lang";

    public Localizer(IHttpContextAccessor accessor)
        : this(accessor.HttpContext?.Items[ItemKey] as string ?? "ar")
    {
    }

    /// <summary>Resolves the request language (cookie, else campaign default) once per request.</summary>
    public static async Task ResolveAsync(HttpContext context, SettingsService settings)
    {
        var requested = context.Request.Cookies[CookieName];
        context.Items[ItemKey] = requested is "ar" or "en"
            ? requested
            : (await settings.GetAsync(context.RequestAborted)).DefaultLanguage is "en" ? "en" : "ar";
    }

    /// <summary>For tests and non-HTTP contexts.</summary>
    public Localizer(string lang) => Lang = lang == "en" ? "en" : "ar";

    public string Lang { get; }
    public bool IsArabic => Lang == "ar";
    public string Dir => IsArabic ? "rtl" : "ltr";
    public string OtherLang => IsArabic ? "en" : "ar";

    public string this[string key] =>
        Texts.TryGetValue(key, out var t) ? (IsArabic ? t.Ar : t.En) : key;

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, this[key], args);

    /// <summary>Pick the language-appropriate value, falling back to English.</summary>
    public string Pick(string en, string? ar) => IsArabic && !string.IsNullOrWhiteSpace(ar) ? ar : en;

    public string Date(DateTime utc)
    {
        var local = CairoTime.ToCairo(utc);
        return IsArabic
            ? local.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("ar-EG"))
            : local.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Strings needed by client-side scripts.</summary>
    public Dictionary<string, string> ForClient(params string[] prefixes) =>
        Texts.Where(kv => prefixes.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal)))
            .ToDictionary(kv => kv.Key, kv => IsArabic ? kv.Value.Ar : kv.Value.En);

    private static readonly Dictionary<string, (string En, string Ar)> Texts = new()
    {
        ["brand.name"] = ("SAMYR TECH WAY", "سمير تك واي"),
        ["brand.headline"] = ("YOUR PURCHASE. YOUR MOMENT. YOUR REWARD.", "YOUR PURCHASE. YOUR MOMENT. YOUR REWARD."),
        ["brand.headline.local"] = ("A laptop purchase unlocks one chance to win a branded gift.", "شراؤك. لحظتك. مكافأتك."),
        ["brand.kicker"] = ("Purchase reward · Spin & Win", "مكافأة الشراء · لِف واكسب"),
        ["brand.tagline"] = ("EL-MOSTAWRYD — Laptop Trading", "المستورد — تجارة اللابتوب"),

        ["nav.lang"] = ("العربية", "English"),
        ["nav.lang.aria"] = ("Switch to Arabic", "التحويل للإنجليزية"),
        ["nav.logout"] = ("Sign out", "خروج"),
        ["nav.play"] = ("Wheel", "العجلة"),
        ["nav.prizes"] = ("My prizes", "جوايزي"),
        ["nav.sound.on"] = ("Sound on", "الصوت شغّال"),
        ["nav.sound.off"] = ("Sound off", "الصوت مقفول"),
        ["nav.skip"] = ("Skip to content", "انتقل للمحتوى"),

        ["login.title"] = ("Sign in to spin", "ادخل وابدأ اللفّة"),
        ["login.lead"] = ("Use the mobile number you gave at purchase and the private access code from your store card.", "استخدم رقم الموبايل اللي سجّلته وقت الشراء وكود الدخول الخاص من كارت الفرع."),
        ["login.phone"] = ("Mobile number", "رقم الموبايل"),
        ["login.phone.hint"] = ("e.g.", "مثال:"),
        ["login.code"] = ("Access code", "كود الدخول"),
        ["login.code.hint"] = ("10 letters and numbers, e.g.", "١٠ حروف وأرقام، مثال:"),
        ["login.code.show"] = ("Show code", "إظهار الكود"),
        ["login.code.hide"] = ("Hide code", "إخفاء الكود"),
        ["login.submit"] = ("Enter & spin", "ادخل ولِف"),
        ["login.submitting"] = ("Checking…", "بنتأكد…"),
        ["login.continue"] = ("Continue to the wheel", "كمّل على العجلة"),
        ["login.signedin"] = ("You're signed in as {0}.", "إنت داخل باسم {0}."),
        ["login.error.rejected"] = ("We couldn't sign you in with that phone number and access code. Check both and try again — after several wrong tries, sign-in pauses for 15 minutes.", "مقدرناش ندخّلك بالرقم والكود دول. راجعهم وجرّب تاني — بعد كذا محاولة غلط، الدخول بيتوقف ١٥ دقيقة."),
        ["login.error.inactive"] = ("This account is paused. Please contact SAMYR TECH WAY sales.", "الحساب ده متوقف حاليًا. من فضلك كلّم مبيعات سمير تك واي."),
        ["login.error.rate"] = ("Too many attempts from this device. Please wait a minute and try again.", "محاولات كتير من الجهاز ده. استنى دقيقة وجرّب تاني."),
        ["login.error.phone"] = ("Enter an Egyptian mobile number starting with 010, 011, 012 or 015.", "اكتب رقم موبايل مصري يبدأ بـ 010 أو 011 أو 012 أو 015."),
        ["login.error.code"] = ("Access codes are 10 letters and numbers.", "كود الدخول ١٠ حروف وأرقام."),
        ["login.noaccount"] = ("No sign-up needed: our store team registers you when you buy a laptop and hands you your private access code.", "مفيش تسجيل: فريق الفرع بيسجّلك وقت شراء اللابتوب ويديك كود الدخول الخاص بيك."),
        ["login.steps.1"] = ("Buy your laptop at SAMYR TECH WAY", "اشتري لابتوبك من سمير تك واي"),
        ["login.steps.2"] = ("Get your private access code", "خد كود الدخول الخاص بيك"),
        ["login.steps.3"] = ("Spin the wheel for a gift", "لِف العجلة واكسب هدية"),

        ["insecure.banner"] = ("Demo connection — secure HTTPS is not active yet. Do not enter real customer codes.", "اتصال تجريبي — الاتصال الآمن (HTTPS) لسه مش مفعّل. متكتبش أكواد حقيقية."),

        ["terms.title"] = ("Promotion terms", "شروط العرض"),
        ["terms.1"] = ("One spin credit per eligible laptop purchase, issued by SAMYR TECH WAY staff.", "لفّة واحدة لكل شراء لابتوب مؤهل، والفرع هو اللي بيضيفها."),
        ["terms.2"] = ("Prizes are branded gifts and accessories — never cash, and not exchangeable for cash.", "الجوايز هدايا وإكسسوارات — مش فلوس، ومينفعش تتبدّل بفلوس."),
        ["terms.3"] = ("A “Try Again” result doesn't use up your spin: you spin again straight away.", "لو طلعلك «حاول تاني» لفّتك مش بتضيع: بتلف تاني على طول."),
        ["terms.4"] = ("Gifts are subject to availability and are collected from the store.", "الهدايا حسب المتاح وبتستلمها من الفرع."),
        ["privacy.title"] = ("Privacy", "الخصوصية"),
        ["privacy.body"] = ("We keep only your name, mobile number and purchase reference to run this promotion. Your number never appears publicly or in shared images, and we use no tracking or ads.", "بنحتفظ باسمك ورقم موبايلك ومرجع الشراء بس عشان العرض ده. رقمك عمره ما يظهر لحد ولا في الصور اللي بتشاركها، ومبنستخدمش تتبّع أو إعلانات."),

        ["play.greeting"] = ("Welcome, {0}", "أهلًا يا {0}"),
        ["play.credits"] = ("Spin credits", "لفّاتك"),
        ["play.spin"] = ("SPIN", "SPIN"),
        ["play.spin.sub"] = ("Tap to spin", "دوس ولِف"),
        ["play.spin.aria"] = ("Spin the wheel", "لِف العجلة"),
        ["play.spinning"] = ("Spinning…", "العجلة بتلف…"),
        ["play.hint"] = ("Your result is decided securely on our server the moment you press SPIN.", "النتيجة بتتحدد بأمان على السيرفر أول ما تدوس SPIN."),
        ["play.legend.title"] = ("What's on the wheel", "إيه اللي على العجلة"),
        ["play.legend.retry"] = ("Free spin again", "لفّة تانية مجانًا"),
        ["play.legend.toggle"] = ("Prizes", "الجوايز"),
        ["play.loading"] = ("Preparing your wheel…", "بنجهّز العجلة…"),
        ["play.nocredits.title"] = ("You've played your spin", "إنت لعبت لفّتك"),
        ["play.nocredits.body"] = ("Thank you for choosing SAMYR TECH WAY. Only our store team can add another spin — for example with your next purchase.", "شكرًا إنك اخترت سمير تك واي. فريق الفرع بس هو اللي يقدر يضيفلك لفّة تانية — مثلًا مع شرائك الجاي."),
        ["play.paused.title"] = ("The wheel is resting", "العجلة واخدة استراحة"),
        ["play.unavailable.title"] = ("Back in a moment", "راجعين حالًا"),
        ["play.unavailable.body"] = ("We're refreshing the prizes on the wheel. Your spin is safe — please check back shortly.", "بنجدّد الجوايز اللي على العجلة. لفّتك محفوظة — ارجع بعد شوية."),
        ["play.inactive.body"] = ("This account is paused. Please contact SAMYR TECH WAY sales.", "الحساب ده متوقف حاليًا. من فضلك كلّم مبيعات سمير تك واي."),
        ["play.offline"] = ("Connection lost. Tap Retry — if your spin was already recorded, you'll see that same result.", "النت فصل. دوس «إعادة المحاولة» — لو لفّتك اتسجّلت، هتشوف نفس النتيجة."),
        ["play.retry"] = ("Retry", "إعادة المحاولة"),
        ["play.error"] = ("Something went wrong. Your credits are safe — please try again.", "حصلت مشكلة. لفّاتك في أمان — جرّب تاني."),
        ["play.session"] = ("Your session ended. Please sign in again.", "الجلسة خلصت. ادخل تاني من فضلك."),
        ["play.rate"] = ("Easy — that was a lot of taps. Wait a few seconds and try again.", "براحة — دوسات كتير. استنى ثواني وجرّب تاني."),
        ["play.last"] = ("Your latest prize", "آخر جايزة ليك"),
        ["play.lastspin"] = ("Last spin", "آخر لفّة"),
        ["play.restored"] = ("Here's the result of your last spin.", "دي نتيجة لفّتك الأخيرة."),

        ["result.won.kicker"] = ("YOU WON", "مبروك! كسبت"),
        ["result.tier.Standard"] = ("A gift for you", "هدية ليك"),
        ["result.tier.Rare"] = ("Rare win!", "مكسب نادر!"),
        ["result.tier.Epic"] = ("Epic win!", "مكسب أسطوري!"),
        ["result.thanks"] = ("Thank you for buying with SAMYR TECH WAY.", "شكرًا إنك اشتريت من سمير تك واي."),
        ["result.pickup"] = ("How to collect", "إزاي تستلم"),
        ["result.share"] = ("Share your win", "شارك مكسبك"),
        ["result.done"] = ("Done", "تمام"),
        ["result.history"] = ("My prizes", "جوايزي"),
        ["result.retry.kicker"] = ("SO CLOSE!", "قرّبت جدًا!"),
        ["result.retry.title"] = ("ONE MORE SPIN", "لفّة كمان"),
        ["result.retry.body"] = ("No prize yet — your spin came straight back to you. Spin again!", "لسه مفيش جايزة — لفّتك رجعتلك تاني. لِف كمان!"),
        ["result.retry.cta"] = ("SPIN AGAIN", "لِف تاني"),
        ["result.credits.left"] = ("Spins left: {0}", "لفّات باقية: {0}"),

        ["prizes.title"] = ("My prizes", "جوايزي"),
        ["prizes.lead"] = ("Every gift you've won with SAMYR TECH WAY.", "كل الهدايا اللي كسبتها مع سمير تك واي."),
        ["prizes.empty"] = ("No prizes yet. Your wins will appear here.", "لسه مفيش جوايز. مكاسبك هتظهر هنا."),
        ["prizes.status.Pending"] = ("Ready to collect", "جاهزة للاستلام"),
        ["prizes.status.Delivered"] = ("Collected", "اتسلّمت"),
        ["prizes.wonon"] = ("Won on {0}", "اتكسبت يوم {0}"),
        ["prizes.history"] = ("Spin history", "سجل اللفّات"),
        ["prizes.attempt.Won"] = ("Won", "كسب"),
        ["prizes.attempt.TryAgain"] = ("Try again", "حاول تاني"),
        ["prizes.back"] = ("Back to the wheel", "ارجع للعجلة"),

        ["share.title"] = ("Share your win", "شارك مكسبك"),
        ["share.name"] = ("Show my first name", "اظهر اسمي الأول"),
        ["share.privacy"] = ("Only your prize and, if you choose, your first name appear — never your number or code.", "بيظهر جايزتك واسمك الأول لو حبيت بس — عمر رقمك أو الكود ما يظهروا."),
        ["share.download"] = ("Download PNG", "تحميل الصورة"),
        ["share.share"] = ("Share…", "مشاركة…"),
        ["share.copy"] = ("Copy image", "نسخ الصورة"),
        ["share.copied"] = ("Image copied", "الصورة اتنسخت"),
        ["share.saved"] = ("Image saved", "الصورة اتحفظت"),
        ["share.generating"] = ("Creating your image…", "بنجهّز صورتك…"),
        ["share.failed"] = ("Couldn't create the image. Please try again.", "مقدرناش نعمل الصورة. جرّب تاني."),
        ["share.close"] = ("Close", "إغلاق"),
        ["share.iwon"] = ("I WON!", "I WON!"),
        ["share.iwon.local"] = ("A gift from SAMYR TECH WAY", "كسبت هدية من سمير تك واي"),
        ["share.text"] = ("I won {0} with SAMYR TECH WAY Spin & Win!", "كسبت {0} مع سمير تك واي — لِف واكسب!"),
        ["share.wonby"] = ("{0} won", "{0} كسب"),

        ["error.400.title"] = ("Page expired", "الصفحة انتهت صلاحيتها"),
        ["error.400.body"] = ("This page was open too long or the form was already sent. Go back, reload and try again.", "الصفحة فضلت مفتوحة كتير أو الطلب اتبعت قبل كده. ارجع، اعمل تحديث للصفحة وجرّب تاني."),
        ["error.404.title"] = ("Page not found", "الصفحة مش موجودة"),
        ["error.404.body"] = ("The page you're looking for isn't here.", "الصفحة اللي بتدوّر عليها مش هنا."),
        ["error.403.title"] = ("Access denied", "مش مسموح"),
        ["error.403.body"] = ("You don't have permission to open this page.", "مش مسموحلك تفتح الصفحة دي."),
        ["error.500.title"] = ("Something went wrong", "حصلت مشكلة"),
        ["error.500.body"] = ("We hit an unexpected problem. Your credits and prizes are safe. Please try again in a moment.", "حصلت مشكلة غير متوقعة. لفّاتك وجوايزك في أمان. جرّب تاني بعد شوية."),
        ["error.home"] = ("Back to home", "ارجع للرئيسية"),

        ["footer.rights"] = ("SAMYR TECH WAY · EL-MOSTAWRYD — Laptop Trading", "سمير تك واي · المستورد — تجارة اللابتوب"),
        ["footer.note"] = ("A purchase reward promotion. Not a lottery; no purchase of chances.", "عرض مكافأة على الشراء. مش يانصيب، ومفيش بيع لفّات."),
    };
}
