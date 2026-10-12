using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ErrorController(Localizer t) : Controller
{
    [Route("/error/{code:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Index(int code)
    {
        var key = code switch { 400 => "400", 404 => "404", 403 => "403", _ => "500" };
        var originalPath = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()?.OriginalPath
                           ?? HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>()?.Path
                           ?? Request.Path.Value ?? "";
        var isAdmin = originalPath.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
        Response.StatusCode = code is >= 400 and < 600 ? code : 500;

        if (originalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return new ObjectResult(new { error = key switch { "400" => "badRequest", "404" => "notFound", "403" => "forbidden", _ => "serverError" } }) { StatusCode = Response.StatusCode };

        // The admin shell (navigation etc.) is only for a signed-in admin; anyone else gets the minimal sign-in layout.
        var adminSignedIn = isAdmin && (await HttpContext.AuthenticateAsync(AuthSchemes.Admin)).Succeeded;
        var model = isAdmin
            ? new ErrorViewModel(Response.StatusCode,
                key switch { "400" => "Form expired", "404" => "Page not found", "403" => "Access denied", _ => "Something went wrong" },
                key switch
                {
                    "400" => "This form was open too long or was already submitted. Go back, reload the page and try again.",
                    "404" => "That page doesn't exist.",
                    "403" => "You don't have permission for this page.",
                    _ => "An unexpected error occurred. It has been logged.",
                }, true, adminSignedIn)
            : new ErrorViewModel(Response.StatusCode, t[$"error.{key}.title"], t[$"error.{key}.body"], false);
        return View("~/Views/Shared/Error.cshtml", model);
    }
}
