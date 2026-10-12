using Microsoft.AspNetCore.Mvc;
using SamyrTechWay.SpinWin.Models.ViewModels;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Areas.Admin.Controllers;

[Route("admin/customers")]
public sealed class CustomersController(AdminQueries queries, CustomerAdminService customers) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CustomerListFilter filter, CancellationToken ct) =>
        View(await queries.CustomersAsync(filter, ct));

    [HttpGet("create")]
    public IActionResult Create() => View(new CustomerForm());

    [HttpPost("create")]
    public async Task<IActionResult> Create(CustomerForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await customers.CreateAsync(form.ToInput(), form.InitialCredits, Actor, ct);
        if (!result.Ok)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
            return View(form);
        }

        var c = result.Value!.Customer;
        // Rendered directly (no redirect, no TempData) so the plaintext code is never stored anywhere.
        return View("CodeReveal", new AccessCodeReveal(c.Id, c.FullName, PhoneNumber.ToLocalDisplay(c.NormalizedPhone),
            AccessCodes.Format(result.Value.AccessCode), c.SpinCredits, IsNew: true, SiteUrl()));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await queries.CustomerAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var c = await queries.CustomerEntityAsync(id, ct);
        if (c is null) return NotFound();
        ViewData["CustomerId"] = id;
        return View(new CustomerForm
        {
            FullName = c.FullName,
            Phone = PhoneNumber.ToLocalDisplay(c.NormalizedPhone),
            InvoiceReference = c.InvoiceReference,
            LaptopModel = c.LaptopModel,
            Notes = c.Notes,
            RowVersion = Convert.ToBase64String(c.RowVersion),
        });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CustomerForm form, CancellationToken ct)
    {
        ViewData["CustomerId"] = id;
        ModelState.Remove(nameof(CustomerForm.InitialCredits));
        if (!ModelState.IsValid) return View(form);
        var rowVersion = DecodeRowVersion(form.RowVersion);
        if (rowVersion is null) return BadRequest();

        var result = await customers.UpdateAsync(id, form.ToInput(), rowVersion, Actor, ct);
        if (!result.Ok)
        {
            AddError(result);
            return View(form);
        }
        Toast("Customer updated.");
        return Redirect($"/admin/customers/{id}");
    }

    [HttpPost("{id:int}/credits/clear")]
    public async Task<IActionResult> ClearCredits(int id, CancellationToken ct)
    {
        var result = await customers.ClearCreditsAsync(id, Actor, ct);
        if (!result.Ok && result.Error == CustomerAdminService.CustomerNotFoundMessage) return NotFound();
        Toast(!result.Ok ? result.Error!
            : result.Value > 0 ? $"Removed {result.Value} spin{(result.Value == 1 ? "" : "s")}. This customer can't spin until you grant credits again."
            : "This customer already has 0 spins.", result.Ok ? "success" : "error");
        return Redirect($"/admin/customers/{id}");
    }

    [HttpPost("{id:int}/credits")]
    public async Task<IActionResult> Credits(int id, CreditForm form, CancellationToken ct)
    {
        var delta = form.Direction == "revoke" ? -form.Amount : form.Amount;
        var result = ModelState.IsValid
            ? await customers.AdjustCreditsAsync(id, delta, form.Reason, Actor, ct)
            : ServiceResult<int>.Fail(ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Check the form.");

        if (!result.Ok)
        {
            var model = await queries.CustomerAsync(id, ct);
            if (model is null) return NotFound();
            ModelState.AddModelError("CreditForm." + (result.Field ?? "Reason"), result.Error!);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Details", new CustomerDetailsViewModel
            {
                Customer = model.Customer, State = model.State, Ledger = model.Ledger, Attempts = model.Attempts,
                Awards = model.Awards, LedgerReconciles = model.LedgerReconciles, CreditForm = form,
            });
        }

        Toast(delta > 0 ? $"Granted {delta} credit(s). Balance: {result.Value}." : $"Removed {-delta} credit(s). Balance: {result.Value}.");
        return Redirect($"/admin/customers/{id}");
    }

    [HttpPost("{id:int}/reset-code")]
    public async Task<IActionResult> ResetCode(int id, CancellationToken ct)
    {
        var result = await customers.ResetAccessCodeAsync(id, Actor, ct);
        if (!result.Ok) return NotFound();
        var c = await queries.CustomerEntityAsync(id, ct);
        return View("CodeReveal", new AccessCodeReveal(id, c!.FullName, PhoneNumber.ToLocalDisplay(c.NormalizedPhone),
            AccessCodes.Format(result.Value!), c.SpinCredits, IsNew: false, SiteUrl()));
    }

    [HttpPost("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id, bool active, CancellationToken ct)
    {
        var result = await customers.SetActiveAsync(id, active, Actor, ct);
        if (!result.Ok) return NotFound();
        Toast(active ? "Customer re-activated." : "Customer disabled. Their sessions have been signed out.", active ? "success" : "warning");
        return Redirect($"/admin/customers/{id}");
    }

    private string SiteUrl() => $"{Request.Scheme}://{Request.Host}";
}
