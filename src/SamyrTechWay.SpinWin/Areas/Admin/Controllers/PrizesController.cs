using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

[Route("admin/prizes")]
public sealed class PrizesController(AdminQueries queries, WheelItemAdminService items) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await queries.PrizesAsync(ct));

    [HttpGet("create")]
    public IActionResult Create(WheelItemKind kind = WheelItemKind.Prize) =>
        View("Edit", new WheelItemForm
        {
            Kind = kind,
            IconKey = kind switch { WheelItemKind.TryAgain => "retry", WheelItemKind.Lose => "smile", _ => "gift" },
            ColorKey = kind == WheelItemKind.Prize ? "ruby" : "onyx",
        });

    [HttpPost("create")]
    public async Task<IActionResult> Create(WheelItemForm form, CancellationToken ct)
    {
        var image = await ReadImageAsync(form, ct);
        if (!ModelState.IsValid) return View("Edit", form);

        var result = await items.CreateAsync(form.ToInput(), image, Actor, ct);
        if (!result.Ok)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
            return View("Edit", form);
        }
        Toast($"تمت إضافة «{AdminText.Name(result.Value!.NameEn, result.Value.NameAr)}»{(result.Value.IsActive ? "" : " (غير مفعّل — فعّله من جدول توزيع النسب)")}.");
        return Redirect("/admin/prizes");
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var item = await queries.WheelItemAsync(id, ct);
        if (item is null) return NotFound();
        return View(WheelItemForm.From(item));
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, WheelItemForm form, CancellationToken ct)
    {
        form.Id = id;
        var current = await queries.WheelItemAsync(id, ct);
        if (current is null) return NotFound();
        form.Kind = current.Kind;
        form.CurrentImageUrl = WheelPresenter.ImageUrl(current.ImageId);

        var image = await ReadImageAsync(form, ct);
        if (!ModelState.IsValid) return View(form);
        var rowVersion = DecodeRowVersion(form.RowVersion);
        if (rowVersion is null) return BadRequest();

        var result = await items.UpdateAsync(id, form.ToInput(), image, form.RemoveImage, rowVersion, Actor, ct);
        if (!result.Ok)
        {
            AddError(result);
            return View(form);
        }
        Toast(result.Value is int retryWeight
            ? $"تم حفظ «{AdminText.Name(form.NameEn, form.NameAr)}». تم ضبط نسبة «حاول مرة أخرى» إلى {WheelItemAdminService.Percent(retryWeight)} ليبقى مجموع العجلة 100%."
            : $"تم حفظ «{AdminText.Name(form.NameEn, form.NameAr)}».");
        return Redirect("/admin/prizes");
    }

    [HttpPost("allocation")]
    public async Task<IActionResult> Allocation(AllocationForm form, CancellationToken ct)
    {
        if (form.Rows.Any(r => r.Percent * 100m != Math.Floor(r.Percent * 100m)))
        {
            Toast("النسب تقبل رقمين عشريين بحد أقصى.", "error");
            return Redirect("/admin/prizes");
        }
        var rows = form.Rows.Select(r => new AllocationRow(r.Id, r.IsActive, (int)(r.Percent * 100m), r.DisplayOrder)).ToList();
        var result = await items.SaveAllocationAsync(rows, Actor, ct);
        Toast(result.Ok ? "تم حفظ توزيع النسب. العجلة تعمل الآن بهذه النسب." : result.Error!, result.Ok ? "success" : "error");
        return Redirect("/admin/prizes");
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var result = await items.ArchiveAsync(id, Actor, ct);
        Toast(result.Ok ? "تمت أرشفة العنصر مع الاحتفاظ بسجله." : result.Error!, result.Ok ? "success" : "error");
        return Redirect("/admin/prizes");
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        var result = await items.RestoreAsync(id, Actor, ct);
        Toast(result.Ok ? "تمت استعادة العنصر وهو غير مفعّل." : result.Error!, result.Ok ? "success" : "error");
        return Redirect("/admin/prizes");
    }

    private async Task<NewImage?> ReadImageAsync(WheelItemForm form, CancellationToken ct)
    {
        if (form.HasTooManyDecimals) ModelState.AddModelError(nameof(form.WeightPercent), "استخدم رقمين عشريين بحد أقصى (مثل 12.50).");
        if (form.Image is null) return null;
        var (data, type, error) = await ImageValidator.ReadAsync(form.Image, ct);
        if (error is not null)
        {
            ModelState.AddModelError(nameof(form.Image), error);
            return null;
        }
        return new NewImage(data!, type!);
    }
}
