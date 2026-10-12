using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

public sealed class DashboardController(AdminQueries queries) : AdminControllerBase
{
    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await queries.DashboardAsync(ct));
}
