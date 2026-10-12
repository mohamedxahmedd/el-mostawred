using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Controllers;

[Authorize(Policy = Policies.Customer)]
public sealed class PlayController(CustomerQueries customers, Localizer t) : Controller
{
    [HttpGet("/play")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await customers.PlayAsync(User.CustomerId()!.Value, t, ct);
        return model is null ? Redirect("/login") : View(model);
    }

    [HttpGet("/my-prizes")]
    public async Task<IActionResult> Prizes(CancellationToken ct)
    {
        var model = await customers.PrizesAsync(User.CustomerId()!.Value, t, ct);
        return model is null ? Redirect("/login") : View(model);
    }
}
