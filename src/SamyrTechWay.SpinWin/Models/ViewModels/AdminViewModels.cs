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
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), StringLength(200)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordForm
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";

    [Required, DataType(DataType.Password), StringLength(200, MinimumLength = 12), Display(Name = "New password")]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match."), Display(Name = "Confirm new password")]
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
    [Required(ErrorMessage = "Full name is required."), StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Mobile number is required."), StringLength(32), Display(Name = "Mobile number")]
    public string Phone { get; set; } = "";

    [StringLength(60), Display(Name = "Invoice / reference")]
    public string? InvoiceReference { get; set; }

    [StringLength(120), Display(Name = "Laptop model")]
    public string? LaptopModel { get; set; }

    [StringLength(1000), Display(Name = "Internal notes")]
    public string? Notes { get; set; }

    [Range(0, CustomerAdminService.MaxInitialCredits), Display(Name = "Spin credits to grant")]
    public int InitialCredits { get; set; } = 1;

    public string? RowVersion { get; set; }

    public CustomerInput ToInput() => new(FullName, Phone, InvoiceReference, LaptopModel, Notes);
}

public sealed class CreditForm
{
    [Range(1, CustomerAdminService.MaxCreditAdjustment)]
    public int Amount { get; set; } = 1;

    public string Direction { get; set; } = "grant"; // grant | revoke

    [Required(ErrorMessage = "A reason is required."), StringLength(300, MinimumLength = 3)]
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

    [Required(ErrorMessage = "English name is required."), StringLength(80), Display(Name = "Name (English)")]
    public string NameEn { get; set; } = "";

    [StringLength(80), Display(Name = "Name (Arabic)")]
    public string? NameAr { get; set; }

    [StringLength(300), Display(Name = "Short description (English)")]
    public string? DescriptionEn { get; set; }

    [StringLength(300), Display(Name = "Short description (Arabic)")]
    public string? DescriptionAr { get; set; }

    public WheelItemKind Kind { get; set; } = WheelItemKind.Prize;

    [Range(0, 999), Display(Name = "Display order")]
    public int DisplayOrder { get; set; } = 10;

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Weight must be between 0 and 100."), Display(Name = "Probability weight (%)")]
    public decimal WeightPercent { get; set; }

    [Display(Name = "Built-in icon")]
    public string? IconKey { get; set; } = "gift";

    [Display(Name = "Wedge colour")]
    public string ColorKey { get; set; } = "ruby";

    [Range(0, 1_000_000), Display(Name = "Stock (leave empty for unlimited)")]
    public int? StockQuantity { get; set; }

    [Range(0, 1000), Display(Name = "Low-stock alert at")]
    public int LowStockThreshold { get; set; } = 3;

    [Display(Name = "Active on the wheel")]
    public bool IsActive { get; set; }

    [Display(Name = "Celebration tier")]
    public CelebrationTier Tier { get; set; } = CelebrationTier.Standard;

    [StringLength(300), Display(Name = "Collection note (English)")]
    public string? CollectionNoteEn { get; set; }

    [StringLength(300), Display(Name = "Collection note (Arabic)")]
    public string? CollectionNoteAr { get; set; }

    [Display(Name = "Image (JPEG, PNG or WebP, max 2 MB)")]
    public IFormFile? Image { get; set; }

    [Display(Name = "Remove current image")]
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
    [Required, StringLength(120), Display(Name = "Site title")]
    public string SiteTitle { get; set; } = "";

    [Required, StringLength(300), Display(Name = "Promo subheading (English)")]
    public string PromoSubheadingEn { get; set; } = "";

    [Required, StringLength(300), Display(Name = "Promo subheading (Arabic)")]
    public string PromoSubheadingAr { get; set; } = "";

    [Required, StringLength(500), Display(Name = "Default pickup instructions (English)")]
    public string PickupInstructionsEn { get; set; } = "";

    [Required, StringLength(500), Display(Name = "Default pickup instructions (Arabic)")]
    public string PickupInstructionsAr { get; set; } = "";

    [StringLength(300), Display(Name = "Announcement (English)")]
    public string? AnnouncementEn { get; set; }

    [StringLength(300), Display(Name = "Announcement (Arabic)")]
    public string? AnnouncementAr { get; set; }

    [Required, StringLength(300), Display(Name = "Paused message (English)")]
    public string PausedMessageEn { get; set; } = "";

    [Required, StringLength(300), Display(Name = "Paused message (Arabic)")]
    public string PausedMessageAr { get; set; } = "";

    [Required, RegularExpression("^(ar|en)$"), Display(Name = "Default customer language")]
    public string DefaultLanguage { get; set; } = "ar";

    [Display(Name = "Sound on by default")]
    public bool SoundEnabledByDefault { get; set; }

    [Range(1500, 12000), Display(Name = "Shortest spin (ms)")]
    public int SpinDurationMinMs { get; set; }

    [Range(1500, 12000), Display(Name = "Longest spin (ms)")]
    public int SpinDurationMaxMs { get; set; }

    [Display(Name = "Animation intensity")]
    public AnimationIntensity AnimationIntensity { get; set; }

    [Display(Name = "Campaign open (spins allowed)")]
    public bool CampaignEnabled { get; set; }

    public string? RowVersion { get; set; }
}

public sealed record AuditRow(DateTime CreatedAtUtc, string AdminName, string Action, string TargetType, string? TargetId, string? Details);
