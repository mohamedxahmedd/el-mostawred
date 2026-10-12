using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

[Route("admin/awards")]
public sealed class AwardsController(AdminQueries queries, AwardService awards) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AwardListFilter filter, CancellationToken ct) =>
        View(await queries.AwardsAsync(filter, ct));

    [HttpPost("{id:int}/deliver")]
    public async Task<IActionResult> Deliver(int id, string? note, string? returnUrl, CancellationToken ct)
    {
        var result = await awards.MarkDeliveredAsync(id, note, Actor, ct);
        Toast(result switch
        {
            DeliveryResult.Delivered => "Marked as delivered.",
            DeliveryResult.AlreadyDelivered => "This award was already marked as delivered — nothing changed.",
            _ => "Award not found.",
        }, result == DeliveryResult.Delivered ? "success" : "warning");
        return LocalRedirect(Url.IsLocalUrl(returnUrl) && returnUrl!.StartsWith("/admin") ? returnUrl : "/admin/awards");
    }
}
