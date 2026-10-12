namespace SamyrTechWay.SpinWin.Models.Entities;

public class WheelItem
{
    public int Id { get; set; }
    public string NameEn { get; set; } = "";
    public string? NameAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public WheelItemKind Kind { get; set; } = WheelItemKind.Prize;
    public int DisplayOrder { get; set; }

    /// <summary>Configured probability in basis points (1% = 100). Active items must total 10,000.</summary>
    public int ProbabilityBasisPoints { get; set; }

    public Guid? ImageId { get; set; }
    public PrizeImage? Image { get; set; }
    public string? IconKey { get; set; }

    /// <summary>Wedge colour key from the brand palette (ruby, flame, ember, amber, gold, onyx).</summary>
    public string ColorKey { get; set; } = "ruby";

    /// <summary>Null = unlimited. Only meaningful for prizes.</summary>
    public int? StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 3;

    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public CelebrationTier CelebrationTier { get; set; } = CelebrationTier.Standard;
    public string? CollectionNoteEn { get; set; }
    public string? CollectionNoteAr { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public bool IsTryAgain => Kind == WheelItemKind.TryAgain;
}
