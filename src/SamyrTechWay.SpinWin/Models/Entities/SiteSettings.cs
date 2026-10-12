namespace SamyrTechWay.SpinWin.Models.Entities;

/// <summary>Single-row campaign settings (Id = 1). The brand logo is a static asset and is deliberately not configurable here.</summary>
public class SiteSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string SiteTitle { get; set; } = "SAMYR TECH WAY — Spin & Win";
    public string PromoSubheadingEn { get; set; } = "";
    public string PromoSubheadingAr { get; set; } = "";
    public string PickupInstructionsEn { get; set; } = "";
    public string PickupInstructionsAr { get; set; } = "";
    public string? AnnouncementEn { get; set; }
    public string? AnnouncementAr { get; set; }
    public string PausedMessageEn { get; set; } = "";
    public string PausedMessageAr { get; set; } = "";
    public string DefaultLanguage { get; set; } = "ar";
    public bool SoundEnabledByDefault { get; set; } = true;
    public int SpinDurationMinMs { get; set; } = 5200;
    public int SpinDurationMaxMs { get; set; } = 6800;
    public AnimationIntensity AnimationIntensity { get; set; } = AnimationIntensity.Standard;
    public bool CampaignEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
