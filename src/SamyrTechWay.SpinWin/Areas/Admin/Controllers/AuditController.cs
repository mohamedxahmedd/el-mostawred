using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

[Route("admin/audit")]
public sealed class AuditController(AdminQueries queries) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await queries.AuditAsync(page, ct));
}
