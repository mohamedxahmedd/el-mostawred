using System.ComponentModel.DataAnnotations;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Models.ViewModels;

public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < Pages;
}

// ---------- Auth ----------
public sealed class AdminLoginForm
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب."), EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح."), StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "كلمة المرور مطلوبة."), DataType(DataType.Password), StringLength(200)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordForm
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة."), DataType(DataType.Password), Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة."), DataType(DataType.Password), StringLength(200, MinimumLength = 12, ErrorMessage = "كلمة المرور الجديدة يجب ألا تقل عن {2} حرفًا."), Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "أكّد كلمة المرور الجديدة."), DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين."), Display(Name = "تأكيد كلمة المرور الجديدة")]
    public string ConfirmPassword { get; set; } = "";
}

// ---------- Dashboard ----------
public sealed record DayActivity(string Label, int Attempts, int Wins);
public sealed record RecentAttemptRow(Guid Id, DateTime CreatedAtUtc, int CustomerId, string CustomerName, string MaskedPhone, string ItemName, SpinResultKind Kind);
public sealed record PendingAwardRow(int Id, DateTime CreatedAtUtc, int CustomerId, string CustomerName, string PrizeName, CelebrationTier Tier);
public sealed record StockAlertRow(int Id, string Name, int Stock, int Threshold);

public sealed class DashboardViewModel
{
    public int Customers { get; init; }
    public int ActiveCustomers { get; init; }
    public int CustomersWithCredits { get; init; }
    public int AvailableCredits { get; init; }
    public int ConsumedCredits { get; init; }
    public int TotalAttempts { get; init; }
    public int TryAgainAttempts { get; init; }
    public int RealWins { get; init; }
    public int PendingAwards { get; init; }
    public int DeliveredAwards { get; init; }
    public int ActivePrizes { get; init; }
    public bool CampaignEnabled { get; init; }
    public WheelStatus WheelStatus { get; init; }
    public int ConfiguredTotal { get; init; }
    public required IReadOnlyList<RecentAttemptRow> RecentAttempts { get; init; }
    public required IReadOnlyList<PendingAwardRow> RecentPendingAwards { get; init; }
    public required IReadOnlyList<StockAlertRow> StockAlerts { get; init; }
    public required IReadOnlyList<DayActivity> Activity { get; init; }
}

// ---------- Customers ----------
public enum CustomerState
{
    Eligible,
    Finished,
    Disabled,
}

public sealed record CustomerRow(
    int Id, string FullName, string Phone, string? Invoice, string? LaptopModel, int Credits, CustomerState State,
    DateTime CreatedAtUtc, DateTime? LastLoginAtUtc, int Awards);

public sealed class CustomerListFilter
{
    public string? Q { get; set; }
    public string? Status { get; set; } // all | active | disabled
    public bool WithCredits { get; set; }
    public int Page { get; set; } = 1;
}

public sealed class CustomerListViewModel
{
    public required CustomerListFilter Filter { get; init; }
    public required Paged<CustomerRow> Result { get; init; }
}

public sealed class CustomerForm
{
    [Required(ErrorMessage = "الاسم بالكامل مطلوب."), StringLength(120, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "الاسم بالكامل")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "رقم الموبايل مطلوب."), StringLength(32, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "رقم الموبايل")]
    public string Phone { get; set; } = "";

    [StringLength(60, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "رقم الفاتورة / المرجع")]
    public string? InvoiceReference { get; set; }

    [StringLength(120, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "موديل اللابتوب")]
    public string? LaptopModel { get; set; }

    [StringLength(1000, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "ملاحظات داخلية")]
    public string? Notes { get; set; }

    [Range(0, CustomerAdminService.MaxInitialCredits, ErrorMessage = "{0} يجب أن يكون بين {1} و{2}."), Display(Name = "عدد اللفّات الممنوحة")]
    public int InitialCredits { get; set; } = 1;

    public string? RowVersion { get; set; }

    public CustomerInput ToInput() => new(FullName, Phone, InvoiceReference, LaptopModel, Notes);
}

public sealed class CreditForm
{
    [Range(1, CustomerAdminService.MaxCreditAdjustment, ErrorMessage = "عدد اللفّات يجب أن يكون بين {1} و{2}.")]
    public int Amount { get; set; } = 1;

    public string Direction { get; set; } = "grant"; // grant | revoke

    [Required(ErrorMessage = "السبب مطلوب."), StringLength(300, MinimumLength = 3, ErrorMessage = "اكتب سببًا من {2} إلى {1} حرف.")]
    public string Reason { get; set; } = "";
}

public sealed record AccessCodeReveal(int CustomerId, string FullName, string Phone, string Code, int Credits, bool IsNew, string SiteUrl);

public sealed class CustomerDetailsViewModel
{
    public required Customer Customer { get; init; }
    public required CustomerState State { get; init; }
    public required IReadOnlyList<CreditLedgerEntry> Ledger { get; init; }
    public required IReadOnlyList<SpinAttempt> Attempts { get; init; }
    public required IReadOnlyList<Award> Awards { get; init; }
    public CreditForm CreditForm { get; init; } = new();
    public bool LedgerReconciles { get; init; }
}

// ---------- Prizes ----------
public sealed class WheelItemForm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب."), StringLength(80, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "الاسم (إنجليزي)")]
    public string NameEn { get; set; } = "";

    [StringLength(80, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "الاسم (عربي)")]
    public string? NameAr { get; set; }

    [StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "وصف قصير (إنجليزي)")]
    public string? DescriptionEn { get; set; }

    [StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "وصف قصير (عربي)")]
    public string? DescriptionAr { get; set; }

    public WheelItemKind Kind { get; set; } = WheelItemKind.Prize;

    [Range(0, 999, ErrorMessage = "{0} يجب أن يكون بين {1} و{2}."), Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 10;

    [Range(typeof(decimal), "0", "100", ErrorMessage = "النسبة يجب أن تكون بين 0 و100."), Display(Name = "نسبة الفوز (%)")]
    public decimal WeightPercent { get; set; }

    [Display(Name = "الأيقونة")]
    public string? IconKey { get; set; } = "gift";

    [Display(Name = "لون الشريحة")]
    public string ColorKey { get; set; } = "ruby";

    [Range(0, 1_000_000, ErrorMessage = "{0} يجب أن يكون بين {1} و{2}."), Display(Name = "المخزون (اتركه فارغًا لعدد غير محدود)")]
    public int? StockQuantity { get; set; }

    [Range(0, 1000, ErrorMessage = "{0} يجب أن يكون بين {1} و{2}."), Display(Name = "تنبيه انخفاض المخزون عند")]
    public int LowStockThreshold { get; set; } = 3;

    [Display(Name = "مفعّلة على العجلة")]
    public bool IsActive { get; set; }

    [Display(Name = "مستوى الاحتفال")]
    public CelebrationTier Tier { get; set; } = CelebrationTier.Standard;

    [StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "ملاحظة الاستلام (إنجليزي)")]
    public string? CollectionNoteEn { get; set; }

    [StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "ملاحظة الاستلام (عربي)")]
    public string? CollectionNoteAr { get; set; }

    [Display(Name = "الصورة (JPEG أو PNG أو WebP، بحد أقصى 2 ميجابايت)")]
    public IFormFile? Image { get; set; }

    [Display(Name = "حذف الصورة الحالية")]
    public bool RemoveImage { get; set; }

    public string? RowVersion { get; set; }
    public string? CurrentImageUrl { get; set; }

    public int BasisPoints => (int)Math.Round(WeightPercent * 100m, MidpointRounding.AwayFromZero);
    public bool HasTooManyDecimals => WeightPercent * 100m != Math.Floor(WeightPercent * 100m);

    public WheelItemInput ToInput() => new(NameEn, NameAr, DescriptionEn, DescriptionAr, Kind, DisplayOrder, BasisPoints,
        IconKey, ColorKey, StockQuantity, LowStockThreshold, IsActive, Tier, CollectionNoteEn, CollectionNoteAr);

    public static WheelItemForm From(WheelItem i) => new()
    {
        Id = i.Id, NameEn = i.NameEn, NameAr = i.NameAr, DescriptionEn = i.DescriptionEn, DescriptionAr = i.DescriptionAr,
        Kind = i.Kind, DisplayOrder = i.DisplayOrder, WeightPercent = i.ProbabilityBasisPoints / 100m, IconKey = i.IconKey,
        ColorKey = i.ColorKey, StockQuantity = i.StockQuantity, LowStockThreshold = i.LowStockThreshold, IsActive = i.IsActive,
        Tier = i.CelebrationTier, CollectionNoteEn = i.CollectionNoteEn, CollectionNoteAr = i.CollectionNoteAr,
        RowVersion = Convert.ToBase64String(i.RowVersion), CurrentImageUrl = WheelPresenter.ImageUrl(i.ImageId),
    };
}

public sealed class AllocationRowForm
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
    public decimal Percent { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class AllocationForm
{
    public List<AllocationRowForm> Rows { get; set; } = [];
}

public sealed record PrizeRow(WheelItem Item, double EffectiveProbability, bool Eligible, bool LowStock, int Wins);

public sealed class PrizesViewModel
{
    public required IReadOnlyList<PrizeRow> Live { get; init; }
    public required IReadOnlyList<PrizeRow> Archived { get; init; }
    public required WheelOdds Odds { get; init; }
    public required IReadOnlyList<WheelItemDto> Preview { get; init; }
}

// ---------- Spins & awards ----------
public sealed class SpinListFilter
{
    public string? Q { get; set; }
    public string? Kind { get; set; } // all | won | tryagain
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record SpinRow(Guid Id, DateTime CreatedAtUtc, int CustomerId, string CustomerName, string? Invoice,
    string ItemName, SpinResultKind Kind, int CreditsBefore, int CreditsAfter, Guid RequestId, int? AwardId);

public sealed class SpinListViewModel
{
    public required SpinListFilter Filter { get; init; }
    public required Paged<SpinRow> Result { get; init; }
}

public sealed class SpinDetailsViewModel
{
    public required SpinAttempt Attempt { get; init; }
    public required Customer Customer { get; init; }
    public Award? Award { get; init; }
}

public sealed class AwardListFilter
{
    public string? Status { get; set; } = "pending"; // pending | delivered | all
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record AwardRow(int Id, DateTime CreatedAtUtc, int CustomerId, string CustomerName, string MaskedPhone, string? Invoice,
    string PrizeName, CelebrationTier Tier, AwardStatus Status, DateTime? DeliveredAtUtc, string? DeliveredBy, string? Note, Guid SpinAttemptId);

public sealed class AwardListViewModel
{
    public required AwardListFilter Filter { get; init; }
    public required Paged<AwardRow> Result { get; init; }
}

// ---------- Settings & audit ----------
public sealed class SettingsForm
{
    [Required(ErrorMessage = "{0} مطلوب."), StringLength(120, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "عنوان الموقع")]
    public string SiteTitle { get; set; } = "";

    [Required(ErrorMessage = "{0} مطلوب."), StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "العنوان الفرعي للعرض (إنجليزي)")]
    public string PromoSubheadingEn { get; set; } = "";

    [Required(ErrorMessage = "{0} مطلوب."), StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "العنوان الفرعي للعرض (عربي)")]
    public string PromoSubheadingAr { get; set; } = "";

    [Required(ErrorMessage = "{0} مطلوب."), StringLength(500, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "تعليمات الاستلام الافتراضية (إنجليزي)")]
    public string PickupInstructionsEn { get; set; } = "";

    [Required(ErrorMessage = "{0} مطلوب."), StringLength(500, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "تعليمات الاستلام الافتراضية (عربي)")]
    public string PickupInstructionsAr { get; set; } = "";

    [StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "إعلان (إنجليزي)")]
    public string? AnnouncementEn { get; set; }

    [StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "إعلان (عربي)")]
    public string? AnnouncementAr { get; set; }

    [Required(ErrorMessage = "{0} مطلوب."), StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "رسالة الإيقاف المؤقت (إنجليزي)")]
    public string PausedMessageEn { get; set; } = "";

    [Required(ErrorMessage = "{0} مطلوب."), StringLength(300, ErrorMessage = "{0} طويل جدًا (الحد الأقصى {1} حرف)."), Display(Name = "رسالة الإيقاف المؤقت (عربي)")]
    public string PausedMessageAr { get; set; } = "";

    [Required(ErrorMessage = "{0} مطلوب."), RegularExpression("^(ar|en)$", ErrorMessage = "اختر العربية أو الإنجليزية."), Display(Name = "لغة العميل الافتراضية")]
    public string DefaultLanguage { get; set; } = "ar";

    [Display(Name = "الصوت مفعّل افتراضيًا")]
    public bool SoundEnabledByDefault { get; set; }

    [Range(1500, 12000, ErrorMessage = "{0} يجب أن يكون بين {1} و{2}."), Display(Name = "أقصر مدة للّفّة (ملّي ثانية)")]
    public int SpinDurationMinMs { get; set; }

    [Range(1500, 12000, ErrorMessage = "{0} يجب أن يكون بين {1} و{2}."), Display(Name = "أطول مدة للّفّة (ملّي ثانية)")]
    public int SpinDurationMaxMs { get; set; }

    [Display(Name = "قوة الحركة")]
    public AnimationIntensity AnimationIntensity { get; set; }

    [Display(Name = "الحملة مفتوحة (اللفّ مسموح)")]
    public bool CampaignEnabled { get; set; }

    public string? RowVersion { get; set; }
}

public sealed record AuditRow(DateTime CreatedAtUtc, string AdminName, string Action, string TargetType, string? TargetId, string? Details);
