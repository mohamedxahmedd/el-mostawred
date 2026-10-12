using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Controllers;

/// <summary>
/// JSON endpoints for the wheel. Cookie-authenticated (customer scheme) and antiforgery-protected
/// (X-CSRF-TOKEN header). The customer id always comes from the auth cookie, never from the request.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.Customer)]
[Route("api")]
public sealed class SpinApiController(SpinService spins, CustomerQueries customers, SettingsService settings, Localizer t) : ControllerBase
{
    private int CustomerId => User.CustomerId()!.Value;

    [HttpGet("wheel/config")]
    public async Task<ActionResult<WheelConfigResponse>> Config(CancellationToken ct) =>
        await customers.WheelConfigAsync(CustomerId, t, ct);

    /// <summary>201 = new committed result; 200 = replay of an existing result for the same requestId.</summary>
    [HttpPost("spins")]
    [EnableRateLimiting(RateLimits.Spin)]
    public async Task<IActionResult> Spin([FromBody] SpinRequest? request, CancellationToken ct)
    {
        if (request is null || request.RequestId == Guid.Empty)
            return Problem400("invalidRequest", t["play.error"]);

        var outcome = await spins.SpinAsync(CustomerId, request.RequestId, ct);
        if (outcome.Succeeded)
        {
            var body = await ToResponseAsync(outcome, ct);
            return outcome.Replayed ? Ok(body) : StatusCode(StatusCodes.Status201Created, body);
        }

        var site = await settings.GetAsync(ct);
        return outcome.Failure switch
        {
            SpinFailure.InvalidRequest => Problem400("invalidRequest", t["play.error"]),
            SpinFailure.CustomerNotFound => Unauthorized(new { error = "unauthenticated", message = t["play.session"] }),
            SpinFailure.CustomerInactive => StatusCode(StatusCodes.Status403Forbidden, new { error = "inactive", message = t["play.inactive.body"], credits = outcome.Credits }),
            SpinFailure.NoCredits => Conflict(new { error = "noCredits", message = t["play.nocredits.body"], credits = outcome.Credits }),
            SpinFailure.CampaignPaused => Conflict(new { error = "paused", message = t.Pick(site.PausedMessageEn, site.PausedMessageAr), credits = outcome.Credits }),
            SpinFailure.WheelUnavailable => Conflict(new { error = "unavailable", message = t["play.unavailable.body"], credits = outcome.Credits }),
            _ => Problem400("invalidRequest", t["play.error"]),
        };
    }

    /// <summary>Side-effect-free lookup used after a reload/network failure to recover a committed result.</summary>
    [HttpGet("spins/{requestId:guid}")]
    public async Task<IActionResult> Lookup(Guid requestId, CancellationToken ct)
    {
        var outcome = await spins.FindReplayAsync(CustomerId, requestId, ct);
        return outcome is null ? NotFound(new { error = "notFound" }) : Ok(await ToResponseAsync(outcome, ct));
    }

    private async Task<SpinResponse> ToResponseAsync(SpinOutcome outcome, CancellationToken ct)
    {
        var a = outcome.Attempt!;
        var site = await settings.GetAsync(ct);
        SpinAwardDto? award = null;
        if (outcome.Award is { } aw)
        {
            var card = CustomerQueries.Card(aw, site, t);
            award = new SpinAwardDto(aw.Id, aw.Status.ToString(), card.Pickup);
        }
        var layout = outcome.DrawLayout is { Count: > 0 } ids ? await customers.LayoutAsync(ids, t, ct) : null;
        return new SpinResponse(a.Id, a.RequestId, outcome.Replayed, a.Kind == Models.Entities.SpinResultKind.Won ? "won" : "tryAgain",
            a.WheelItemId, WheelPresenter.Prize(a, t), outcome.Credits, award, layout, t.Date(a.CreatedAtUtc));
    }

    private BadRequestObjectResult Problem400(string error, string message) => BadRequest(new { error, message });
}
