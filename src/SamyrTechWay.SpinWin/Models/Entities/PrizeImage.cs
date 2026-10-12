namespace SamyrTechWay.SpinWin.Models.Entities;

/// <summary>
/// Uploaded prize artwork, stored in SQL so redeploys never lose it. Rows are immutable:
/// replacing an image creates a new row, so attempt/award snapshots keep pointing at the original.
/// </summary>
public class PrizeImage
{
    public Guid Id { get; set; }
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public int ByteLength { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
