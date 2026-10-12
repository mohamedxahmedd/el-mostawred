using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public sealed class SettingsService(AppDbContext db, IMemoryCache cache)
{
    private const string CacheKey = "stw:settings";

    /// <summary>Read-only settings snapshot (cached briefly; invalidated on save).</summary>
    public async Task<SiteSettings> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out SiteSettings? cached) && cached is not null) return cached;
        var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SiteSettings.SingletonId, ct)
                       ?? Defaults();
        cache.Set(CacheKey, settings, TimeSpan.FromSeconds(30));
        return settings;
    }

    public void Invalidate() => cache.Remove(CacheKey);

    public static SiteSettings Defaults() => new()
    {
        Id = SiteSettings.SingletonId,
        SiteTitle = "SAMYR TECH WAY — Spin & Win",
        PromoSubheadingEn = "Your new laptop unlocks one spin for a branded SAMYR TECH WAY gift.",
        PromoSubheadingAr = "لابتوبك الجديد بيفتحلك لفّة على العجلة لهدية من سمير تك واي.",
        PickupInstructionsEn = "Visit the SAMYR TECH WAY store with your ID and the phone number you registered, or contact our sales team to arrange collection.",
        PickupInstructionsAr = "تقدر تستلم هديتك من فرع سمير تك واي ومعاك بطاقتك ورقم الموبايل المسجّل، أو كلّم فريق المبيعات عشان نرتّب الاستلام.",
        PausedMessageEn = "The wheel is resting for a moment. Your spins are safe — please check back soon.",
        PausedMessageAr = "العجلة واخدة استراحة قصيرة. لفّاتك محفوظة — ارجع لنا بعد شوية.",
        DefaultLanguage = "ar",
        SoundEnabledByDefault = true,
        SpinDurationMinMs = 5200,
        SpinDurationMaxMs = 6800,
        AnimationIntensity = AnimationIntensity.Standard,
        CampaignEnabled = true,
    };
}

public sealed class SettingsAdminService(AppDbContext db, SettingsService settings, AuditService audit, TimeProvider clock)
{
    public async Task<ServiceResult> UpdateAsync(Action<SiteSettings> apply, byte[]? rowVersion, AdminActor actor, CancellationToken ct = default)
    {
        var entity = await db.SiteSettings.FirstOrDefaultAsync(s => s.Id == SiteSettings.SingletonId, ct);
        var isNew = entity is null;
        entity ??= SettingsService.Defaults();
        if (!isNew && rowVersion is not null) db.Entry(entity).Property(s => s.RowVersion).OriginalValue = rowVersion;

        var before = new { entity.CampaignEnabled, entity.DefaultLanguage, entity.SoundEnabledByDefault, entity.SpinDurationMinMs, entity.SpinDurationMaxMs, intensity = entity.AnimationIntensity.ToString() };
        apply(entity);
        if (entity.SpinDurationMinMs > entity.SpinDurationMaxMs)
            return ServiceResult.Fail("The shortest spin must not be longer than the longest spin.", "SpinDurationMinMs");
        entity.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        if (isNew) db.SiteSettings.Add(entity);

        audit.Record(actor, "settings.update", "SiteSettings", "1", new
        {
            before,
            after = new { entity.CampaignEnabled, entity.DefaultLanguage, entity.SoundEnabledByDefault, entity.SpinDurationMinMs, entity.SpinDurationMaxMs, intensity = entity.AnimationIntensity.ToString() },
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("Settings were changed by another admin. Reload the page and try again.");
        }
        settings.Invalidate();
        return ServiceResult.Success;
    }
}
