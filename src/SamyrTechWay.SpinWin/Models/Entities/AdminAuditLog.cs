namespace SamyrTechWay.SpinWin.Models.Entities;

/// <summary>Admin action trail. Details never contain access codes, passwords or hashes.</summary>
public class AdminAuditLog
{
    public long Id { get; set; }
    public string? AdminUserId { get; set; }
    public string AdminName { get; set; } = "";
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string? TargetId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
