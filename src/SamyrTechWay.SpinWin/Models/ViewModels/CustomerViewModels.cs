using System.ComponentModel.DataAnnotations;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Models.ViewModels;

public sealed class CustomerLoginForm
{
    [Required, StringLength(32)]
    public string Phone { get; set; } = "";

    [Required, StringLength(40)]
    public string AccessCode { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class LandingViewModel
{
    public required CustomerLoginForm Form { get; init; }
    public string? Error { get; init; }
    public string? SignedInFirstName { get; init; }
    public required string Subheading { get; init; }
    public string? Announcement { get; init; }
    public bool InsecureConnection { get; init; }
}

public sealed record AwardCard(
    int Id,
    string Name,
    string Tier,
    string? ImageUrl,
    string Icon,
    string Color,
    string Status,
    DateTime WonAtUtc,
    string WonOn,
    string Pickup);

public sealed record AttemptRow(DateTime CreatedAtUtc, string When, string Name, string Kind);

public enum PlayStatus
{
    Ready,
    NoCredits,
    Paused,
    Unavailable,
}

public sealed class PlayViewModel
{
    public required string FirstName { get; init; }
    public required int Credits { get; init; }
    public required PlayStatus Status { get; init; }
    public string? PausedMessage { get; init; }
    public string? Announcement { get; init; }
    public AwardCard? LatestAward { get; init; }
    public required bool SoundDefault { get; init; }
    public required int SpinMinMs { get; init; }
    public required int SpinMaxMs { get; init; }
    public required string Intensity { get; init; }
    public required string Pickup { get; init; }
}

public sealed class MyPrizesViewModel
{
    public required string FirstName { get; init; }
    public required IReadOnlyList<AwardCard> Awards { get; init; }
    public required IReadOnlyList<AttemptRow> Attempts { get; init; }
}

public sealed record WheelConfigResponse(
    string Status,
    bool CanSpin,
    string? Message,
    int Credits,
    IReadOnlyList<WheelItemDto> Items);

public sealed class SpinRequest
{
    public Guid RequestId { get; set; }
}

public sealed record SpinAwardDto(int Id, string Status, string Pickup);

public sealed record SpinResponse(
    Guid AttemptId,
    Guid RequestId,
    bool Replayed,
    string Result,
    int ItemId,
    PrizeDto Item,
    int Credits,
    SpinAwardDto? Award,
    IReadOnlyList<WheelItemDto>? Layout,
    string WonOn);

/// <param name="IsAdmin">The failed request was under /admin.</param>
/// <param name="AdminSignedIn">An admin is signed in, so the full admin layout may be shown.</param>
public sealed record ErrorViewModel(int StatusCode, string Title, string Message, bool IsAdmin, bool AdminSignedIn = false);
