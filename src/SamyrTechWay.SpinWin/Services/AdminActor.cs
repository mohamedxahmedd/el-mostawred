using System.Security.Claims;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>The admin performing an action, captured for audit trails.</summary>
public sealed record AdminActor(string Id, string Name)
{
    public static AdminActor From(ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Admin identity missing."),
        user.FindFirstValue(AppClaims.DisplayName) ?? user.Identity?.Name ?? "admin");
}

public static class AppClaims
{
    public const string DisplayName = "stw:name";
    public const string Role = "stw:role";
    public const string AdminRole = "admin";
    public const string CustomerRole = "customer";
    public const string AccessCodeVersion = "stw:cv";
    public const string SecurityStamp = "stw:ss";
}
