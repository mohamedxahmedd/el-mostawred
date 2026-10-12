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
                key switch { "400" => "انتهت صلاحية النموذج", "404" => "الصفحة غير موجودة", "403" => "غير مسموح بالدخول", _ => "حدث خطأ" },
                key switch
                {
                    "400" => "ظل هذا النموذج مفتوحًا لفترة طويلة أو تم إرساله بالفعل. ارجع، وحدّث الصفحة، وحاول مرة أخرى.",
                    "404" => "هذه الصفحة غير موجودة.",
                    "403" => "ليس لديك صلاحية لفتح هذه الصفحة.",
                    _ => "حدث خطأ غير متوقع وتم تسجيله. حاول مرة أخرى بعد قليل.",
                }, true, adminSignedIn)
            : new ErrorViewModel(Response.StatusCode, t[$"error.{key}.title"], t[$"error.{key}.body"], false);
        return View("~/Views/Shared/Error.cshtml", model);
    }
}
