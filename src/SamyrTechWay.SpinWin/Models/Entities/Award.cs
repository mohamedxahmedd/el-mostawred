namespace SamyrTechWay.SpinWin.Models.Entities;

public class Award
{
    public int Id { get; set; }
    public Guid SpinAttemptId { get; set; }
    public SpinAttempt? SpinAttempt { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int WheelItemId { get; set; }

    // Immutable prize snapshot.
    public string PrizeNameEn { get; set; } = "";
    public string? PrizeNameAr { get; set; }
    public Guid? ImageId { get; set; }
    public string? IconKey { get; set; }
    public string ColorKey { get; set; } = "";
    public CelebrationTier CelebrationTier { get; set; }
    public string? CollectionNoteEn { get; set; }
    public string? CollectionNoteAr { get; set; }

    public AwardStatus Status { get; set; } = AwardStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string? DeliveredByAdminId { get; set; }
    public string? DeliveredByAdminName { get; set; }
    public string? AdminNote { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
