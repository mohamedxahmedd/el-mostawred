using Microsoft.AspNetCore.Identity;

namespace SamyrTechWay.SpinWin.Models.Entities;

public class AdminUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}
