using System.Text.Json;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Adds audit rows to the current unit of work; they commit with the change they describe.</summary>
public sealed class AuditService(AppDbContext db, TimeProvider clock)
{
    public void Record(AdminActor actor, string action, string targetType, string? targetId, object? details = null)
    {
        var json = details is null ? null : JsonSerializer.Serialize(details);
        if (json is { Length: > 1000 }) json = json[..1000];
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = actor.Id,
            AdminName = actor.Name,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = json,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        });
    }
}
