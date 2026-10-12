namespace SamyrTechWay.SpinWin.Models.Entities;

/// <summary>Immutable audit record of one spin. Never updated after insert.</summary>
public class SpinAttempt
{
    public Guid Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int WheelItemId { get; set; }
    public WheelItem? WheelItem { get; set; }

    /// <summary>Client idempotency key. Unique per customer.</summary>
    public Guid RequestId { get; set; }

    public SpinResultKind Kind { get; set; }

    // Snapshot of the wheel item at draw time.
    public string ItemNameEn { get; set; } = "";
    public string? ItemNameAr { get; set; }
    public WheelItemKind ItemKind { get; set; }
    public Guid? ImageId { get; set; }
    public string? IconKey { get; set; }
    public string ColorKey { get; set; } = "";
    public CelebrationTier CelebrationTier { get; set; }
    public int ConfiguredBasisPoints { get; set; }
    public int EffectiveWeight { get; set; }
    public int EffectiveTotalWeight { get; set; }

    public int CreditsBefore { get; set; }
    public int CreditsAfter { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Award? Award { get; set; }
}
