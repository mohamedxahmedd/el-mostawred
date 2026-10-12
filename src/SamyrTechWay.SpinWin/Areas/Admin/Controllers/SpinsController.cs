using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

/// <summary>Read-only: past outcomes can never be edited.</summary>
[Route("admin/spins")]
public sealed class SpinsController(AdminQueries queries) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] SpinListFilter filter, CancellationToken ct) =>
        View(await queries.SpinsAsync(filter, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var model = await queries.SpinAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }
}
