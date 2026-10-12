using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Models.ViewModels;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Read models for the signed-in customer. Every query is scoped to that customer's id.</summary>
public sealed class CustomerQueries(AppDbContext db, SettingsService settings, SpinService spins)
{
    public async Task<Customer?> FindAsync(int customerId, CancellationToken ct) =>
        await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);

    public async Task<PlayViewModel?> PlayAsync(int customerId, Localizer t, CancellationToken ct)
    {
        var customer = await FindAsync(customerId, ct);
        if (customer is null) return null;
        var site = await settings.GetAsync(ct);
        var odds = WheelOdds.Compute(await spins.LoadSnapshotsAsync(ct));
        var latest = await db.Awards.AsNoTracking().Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.CreatedAtUtc).FirstOrDefaultAsync(ct);

        var status = !site.CampaignEnabled ? PlayStatus.Paused
            : customer.SpinCredits <= 0 ? PlayStatus.NoCredits
            : !odds.CanSpin ? PlayStatus.Unavailable
            : PlayStatus.Ready;

        return new PlayViewModel
        {
            FirstName = customer.FirstName,
            Credits = customer.SpinCredits,
            Status = status,
            PausedMessage = t.Pick(site.PausedMessageEn, site.PausedMessageAr),
            Announcement = NullIfEmpty(t.Pick(site.AnnouncementEn ?? "", site.AnnouncementAr)),
            LatestAward = latest is null ? null : Card(latest, site, t),
            SoundDefault = site.SoundEnabledByDefault,
            SpinMinMs = site.SpinDurationMinMs,
            SpinMaxMs = site.SpinDurationMaxMs,
            Intensity = site.AnimationIntensity.ToString(),
            Pickup = t.Pick(site.PickupInstructionsEn, site.PickupInstructionsAr),
        };
    }

    public async Task<WheelConfigResponse> WheelConfigAsync(int customerId, Localizer t, CancellationToken ct)
    {
        var site = await settings.GetAsync(ct);
        var credits = await db.Customers.Where(c => c.Id == customerId).Select(c => c.SpinCredits).FirstOrDefaultAsync(ct);
        var items = await db.WheelItems.AsNoTracking().Where(w => w.IsActive && !w.IsArchived).ToListAsync(ct);
        var odds = WheelOdds.Compute(items.Select(SpinService.ToSnapshot));
        var dtos = odds.CanSpin ? WheelPresenter.Items(items, odds, t) : [];

        var (status, message) = !site.CampaignEnabled ? ("paused", t.Pick(site.PausedMessageEn, site.PausedMessageAr))
            : !odds.CanSpin ? ("unavailable", t["play.unavailable.body"])
            : credits <= 0 ? ("noCredits", t["play.nocredits.body"])
            : ("ready", (string?)null);

        return new WheelConfigResponse(status, status == "ready", message, credits, dtos);
    }

    /// <summary>Presentation of the exact item set (and order) used for a fresh draw, so the wheel matches it.</summary>
    public async Task<IReadOnlyList<WheelItemDto>> LayoutAsync(IReadOnlyList<int> ids, Localizer t, CancellationToken ct)
    {
        var items = await db.WheelItems.AsNoTracking().Where(w => ids.Contains(w.Id)).ToListAsync(ct);
        var ordered = ids.Select(id => items.FirstOrDefault(i => i.Id == id)).OfType<WheelItem>().ToList();
        return WheelPresenter.Items(ordered, t);
    }

    public async Task<MyPrizesViewModel?> PrizesAsync(int customerId, Localizer t, CancellationToken ct)
    {
        var customer = await FindAsync(customerId, ct);
        if (customer is null) return null;
        var site = await settings.GetAsync(ct);
        var awards = await db.Awards.AsNoTracking().Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.CreatedAtUtc).Take(50).ToListAsync(ct);
        var attempts = await db.SpinAttempts.AsNoTracking().Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.CreatedAtUtc).Take(50).ToListAsync(ct);

        return new MyPrizesViewModel
        {
            FirstName = customer.FirstName,
            Awards = awards.Select(a => Card(a, site, t)).ToList(),
            Attempts = attempts.Select(a => new AttemptRow(
                a.CreatedAtUtc,
                CairoTime.Format(a.CreatedAtUtc, "dd/MM/yyyy HH:mm"),
                t.Pick(a.ItemNameEn, a.ItemNameAr),
                a.Kind.ToString())).ToList(),
        };
    }

    public static AwardCard Card(Award a, SiteSettings site, Localizer t)
    {
        var prize = WheelPresenter.Prize(a, t);
        var note = t.Pick(a.CollectionNoteEn ?? "", a.CollectionNoteAr);
        return new AwardCard(a.Id, prize.Name, prize.Tier, prize.ImageUrl, prize.Icon, prize.Color, a.Status.ToString(),
            a.CreatedAtUtc, t.Date(a.CreatedAtUtc),
            string.IsNullOrWhiteSpace(note) ? t.Pick(site.PickupInstructionsEn, site.PickupInstructionsAr) : note);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
