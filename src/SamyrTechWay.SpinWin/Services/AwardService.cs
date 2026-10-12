using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public enum DeliveryResult
{
    Delivered,
    AlreadyDelivered,
    NotFound,
}

public sealed class AwardService(AppDbContext db, AuditService audit, TimeProvider clock)
{
    /// <summary>Pending → Delivered exactly once. A guarded UPDATE makes concurrent clicks safe and idempotent.</summary>
    public async Task<DeliveryResult> MarkDeliveredAsync(int awardId, string? note, AdminActor actor, CancellationToken ct = default)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > 500 }) note = note[..500];

        return await db.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var updated = await db.Awards
                .Where(a => a.Id == awardId && a.Status == AwardStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, AwardStatus.Delivered)
                    .SetProperty(a => a.DeliveredAtUtc, now)
                    .SetProperty(a => a.DeliveredByAdminId, actor.Id)
                    .SetProperty(a => a.DeliveredByAdminName, actor.Name)
                    .SetProperty(a => a.AdminNote, a => note ?? a.AdminNote), token);

            if (updated == 0)
                return await db.Awards.AnyAsync(a => a.Id == awardId, token) ? DeliveryResult.AlreadyDelivered : DeliveryResult.NotFound;

            audit.Record(actor, "award.deliver", "Award", awardId.ToString(), note is null ? null : new { note });
            await db.SaveChangesAsync(token);
            return DeliveryResult.Delivered;
        }, ct);
    }
}
