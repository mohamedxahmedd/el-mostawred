namespace SamyrTechWay.SpinWin.Models.Entities;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";

    /// <summary>International format, e.g. +201012345678. Unique.</summary>
    public string NormalizedPhone { get; set; } = "";

    public string? InvoiceReference { get; set; }
    public string? LaptopModel { get; set; }
    public string? Notes { get; set; }

    /// <summary>Salted PBKDF2 hash of the access code. The plaintext code is never stored.</summary>
    public string AccessCodeHash { get; set; } = "";

    /// <summary>Incremented on every code reset; embedded in the customer cookie so old sessions die.</summary>
    public int AccessCodeVersion { get; set; }

    public int SpinCredits { get; set; }
    public bool IsActive { get; set; } = true;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public string FirstName => FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
}
