using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;

namespace SamyrTechWay.SpinWin.Controllers;

/// <summary>Serves validated prize images from SQL. Images are immutable, so they cache forever.</summary>
public sealed class MediaController(AppDbContext db) : Controller
{
    [HttpGet("/media/prize/{id:guid}")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Prize(Guid id, CancellationToken ct)
    {
        var image = await db.PrizeImages.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new { i.ContentType, i.Data, i.Sha256 })
            .FirstOrDefaultAsync(ct);
        if (image is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ContentDisposition = "inline";
        return File(image.Data, image.ContentType, lastModified: null, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{image.Sha256}\""));
    }
}
