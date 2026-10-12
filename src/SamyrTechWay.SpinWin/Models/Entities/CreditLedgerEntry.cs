namespace SamyrTechWay.SpinWin.Models.Entities;

/// <summary>Append-only credit movements. Sum(Delta) per customer equals Customer.SpinCredits.</summary>
public class CreditLedgerEntry
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public int Delta { get; set; }
    public int BalanceAfter { get; set; }
    public CreditReason Reason { get; set; }
    public Guid? SpinAttemptId { get; set; }
    public string? AdminUserId { get; set; }
    public string? AdminName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
