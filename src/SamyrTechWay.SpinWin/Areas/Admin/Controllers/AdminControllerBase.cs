using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Infrastructure;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

/// <summary>Every admin controller requires the Admin cookie scheme + admin role claim, enforced server-side.</summary>
[Area("Admin")]
[Authorize(Policy = Policies.Admin)]
public abstract class AdminControllerBase : Controller
{
    protected AdminActor Actor => AdminActor.From(User);

    protected void Toast(string message, string kind = "success")
    {
        TempData["toast"] = message;
        TempData["toastKind"] = kind;
    }

    protected void AddError(ServiceResult r) => ModelState.AddModelError(r.Field ?? string.Empty, r.Error ?? "حدث خطأ. حاول مرة أخرى.");
    protected void AddError<T>(ServiceResult<T> r) => AddError(new ServiceResult(r.Ok, r.Error, r.Field));

    protected static byte[]? DecodeRowVersion(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try { return Convert.FromBase64String(value); }
        catch (FormatException) { return null; }
    }
}
