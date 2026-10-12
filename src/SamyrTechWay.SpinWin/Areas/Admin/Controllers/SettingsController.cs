using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

[Route("admin/settings")]
public sealed class SettingsController(AdminQueries queries, SettingsAdminService settings) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var s = await queries.SettingsEntityAsync(ct);
        return View(new SettingsForm
        {
            SiteTitle = s.SiteTitle, PromoSubheadingEn = s.PromoSubheadingEn, PromoSubheadingAr = s.PromoSubheadingAr,
            PickupInstructionsEn = s.PickupInstructionsEn, PickupInstructionsAr = s.PickupInstructionsAr,
            AnnouncementEn = s.AnnouncementEn, AnnouncementAr = s.AnnouncementAr,
            PausedMessageEn = s.PausedMessageEn, PausedMessageAr = s.PausedMessageAr,
            DefaultLanguage = s.DefaultLanguage, SoundEnabledByDefault = s.SoundEnabledByDefault,
            SpinDurationMinMs = s.SpinDurationMinMs, SpinDurationMaxMs = s.SpinDurationMaxMs,
            AnimationIntensity = s.AnimationIntensity, CampaignEnabled = s.CampaignEnabled,
            RowVersion = s.RowVersion.Length > 0 ? Convert.ToBase64String(s.RowVersion) : null,
        });
    }

    [HttpPost("")]
    public async Task<IActionResult> Index(SettingsForm form, CancellationToken ct)
    {
        if (!Enum.IsDefined(form.AnimationIntensity)) ModelState.AddModelError(nameof(form.AnimationIntensity), "Choose an intensity.");
        if (!ModelState.IsValid) return View(form);

        var result = await settings.UpdateAsync(s =>
        {
            s.SiteTitle = form.SiteTitle.Trim();
            s.PromoSubheadingEn = form.PromoSubheadingEn.Trim();
            s.PromoSubheadingAr = form.PromoSubheadingAr.Trim();
            s.PickupInstructionsEn = form.PickupInstructionsEn.Trim();
            s.PickupInstructionsAr = form.PickupInstructionsAr.Trim();
            s.AnnouncementEn = string.IsNullOrWhiteSpace(form.AnnouncementEn) ? null : form.AnnouncementEn.Trim();
            s.AnnouncementAr = string.IsNullOrWhiteSpace(form.AnnouncementAr) ? null : form.AnnouncementAr.Trim();
            s.PausedMessageEn = form.PausedMessageEn.Trim();
            s.PausedMessageAr = form.PausedMessageAr.Trim();
            s.DefaultLanguage = form.DefaultLanguage;
            s.SoundEnabledByDefault = form.SoundEnabledByDefault;
            s.SpinDurationMinMs = form.SpinDurationMinMs;
            s.SpinDurationMaxMs = form.SpinDurationMaxMs;
            s.AnimationIntensity = form.AnimationIntensity;
            s.CampaignEnabled = form.CampaignEnabled;
        }, DecodeRowVersion(form.RowVersion), Actor, ct);

        if (!result.Ok)
        {
            AddError(result);
            return View(form);
        }
        Toast(form.CampaignEnabled ? "Settings saved." : "Settings saved. The campaign is PAUSED — customers cannot spin.", form.CampaignEnabled ? "success" : "warning");
        return Redirect("/admin/settings");
    }
}
