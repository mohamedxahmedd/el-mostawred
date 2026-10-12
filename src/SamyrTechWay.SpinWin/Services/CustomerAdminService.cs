using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public sealed record CustomerInput(
    string FullName,
    string Phone,
    string? InvoiceReference,
    string? LaptopModel,
    string? Notes);

public sealed record CreatedCustomer(Customer Customer, string AccessCode);

public sealed class CustomerAdminService(
    AppDbContext db,
    AccessCodes codes,
    AuditService audit,
    TimeProvider clock)
{
    public const int MaxInitialCredits = 20;
    public const int MaxCreditAdjustment = 20;
    public const string DuplicatePhoneMessage = "A customer with this phone number already exists.";

    public async Task<ServiceResult<CreatedCustomer>> CreateAsync(CustomerInput input, int initialCredits, AdminActor actor, CancellationToken ct = default)
    {
        if (!PhoneNumber.TryNormalize(input.Phone, out var phone))
            return ServiceResult<CreatedCustomer>.Fail("Enter a valid Egyptian mobile number (010, 011, 012 or 015).", nameof(input.Phone));
        if (initialCredits is < 0 or > MaxInitialCredits)
            return ServiceResult<CreatedCustomer>.Fail($"Initial credits must be between 0 and {MaxInitialCredits}.", "InitialCredits");
        if (await db.Customers.AnyAsync(c => c.NormalizedPhone == phone, ct))
            return ServiceResult<CreatedCustomer>.Fail(DuplicatePhoneMessage, nameof(input.Phone));

        var code = AccessCodes.Generate();
        try
        {
            var customer = await db.InTransactionAsync(async token =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var customer = new Customer
                {
                    FullName = Clean(input.FullName)!,
                    NormalizedPhone = phone,
                    InvoiceReference = Clean(input.InvoiceReference),
                    LaptopModel = Clean(input.LaptopModel),
                    Notes = Clean(input.Notes),
                    SpinCredits = initialCredits,
                    IsActive = true,
                    AccessCodeVersion = 1,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                customer.AccessCodeHash = codes.Hash(customer, code);
                db.Customers.Add(customer);
                await db.SaveChangesAsync(token);

                if (initialCredits > 0)
                {
                    db.CreditLedger.Add(new CreditLedgerEntry
                    {
                        CustomerId = customer.Id,
                        Delta = initialCredits,
                        BalanceAfter = initialCredits,
                        Reason = CreditReason.Initial,
                        AdminUserId = actor.Id,
                        AdminName = actor.Name,
                        Note = "Initial purchase credit",
                        CreatedAtUtc = now,
                    });
                }
                audit.Record(actor, "customer.create", "Customer", customer.Id.ToString(),
                    new { initialCredits, invoice = customer.InvoiceReference });
                await db.SaveChangesAsync(token);
                return customer;
            }, ct);
            return ServiceResult<CreatedCustomer>.Success(new CreatedCustomer(customer, code));
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return ServiceResult<CreatedCustomer>.Fail(DuplicatePhoneMessage, nameof(input.Phone));
        }
    }

    public async Task<ServiceResult> UpdateAsync(int id, CustomerInput input, byte[] rowVersion, AdminActor actor, CancellationToken ct = default)
    {
        if (!PhoneNumber.TryNormalize(input.Phone, out var phone))
            return ServiceResult.Fail("Enter a valid Egyptian mobile number (010, 011, 012 or 015).", nameof(input.Phone));

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return ServiceResult.Fail("Customer not found.");
        if (customer.NormalizedPhone != phone && await db.Customers.AnyAsync(c => c.NormalizedPhone == phone && c.Id != id, ct))
            return ServiceResult.Fail(DuplicatePhoneMessage, nameof(input.Phone));

        db.Entry(customer).Property(c => c.RowVersion).OriginalValue = rowVersion;
        var changed = new List<string>();
        void Set<T>(string name, T current, T next, Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(current, next)) return;
            apply(next);
            changed.Add(name);
        }
        Set("FullName", customer.FullName, Clean(input.FullName)!, v => customer.FullName = v);
        Set("Phone", customer.NormalizedPhone, phone, v => customer.NormalizedPhone = v);
        Set("InvoiceReference", customer.InvoiceReference, Clean(input.InvoiceReference), v => customer.InvoiceReference = v);
        Set("LaptopModel", customer.LaptopModel, Clean(input.LaptopModel), v => customer.LaptopModel = v);
        Set("Notes", customer.Notes, Clean(input.Notes), v => customer.Notes = v);
        if (changed.Count == 0) return ServiceResult.Success;

        customer.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        audit.Record(actor, "customer.update", "Customer", id.ToString(), new { fields = changed });
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("This customer was changed by someone else (or by a spin). Reload the page and try again.");
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ServiceResult.Fail(DuplicatePhoneMessage, nameof(input.Phone));
        }
    }

    /// <summary>Grant (positive) or revoke (negative) credits atomically. Balance can never go below zero.</summary>
    public async Task<ServiceResult<int>> AdjustCreditsAsync(int id, int delta, string? reason, AdminActor actor, CancellationToken ct = default)
    {
        reason = Clean(reason);
        if (delta == 0 || Math.Abs(delta) > MaxCreditAdjustment)
            return ServiceResult<int>.Fail($"Choose between 1 and {MaxCreditAdjustment} credits.", "Amount");
        if (reason is null || reason.Length < 3)
            return ServiceResult<int>.Fail("A reason is required for every credit change.", "Reason");

        return await db.InTransactionAsync(async token =>
        {
            var updated = await db.Customers
                .Where(c => c.Id == id && c.SpinCredits + delta >= 0)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.SpinCredits, c => c.SpinCredits + delta), token);
            if (updated == 0)
            {
                var exists = await db.Customers.AnyAsync(c => c.Id == id, token);
                return ServiceResult<int>.Fail(exists ? "Credits cannot go below zero." : "Customer not found.", "Amount");
            }

            var balance = await db.Customers.Where(c => c.Id == id).Select(c => c.SpinCredits).SingleAsync(token);
            var now = clock.GetUtcNow().UtcDateTime;
            db.CreditLedger.Add(new CreditLedgerEntry
            {
                CustomerId = id,
                Delta = delta,
                BalanceAfter = balance,
                Reason = delta > 0 ? CreditReason.AdminGrant : CreditReason.AdminRevoke,
                AdminUserId = actor.Id,
                AdminName = actor.Name,
                Note = reason.Length > 300 ? reason[..300] : reason,
                CreatedAtUtc = now,
            });
            audit.Record(actor, delta > 0 ? "credits.grant" : "credits.revoke", "Customer", id.ToString(),
                new { delta, balance, reason });
            await db.SaveChangesAsync(token);
            return ServiceResult<int>.Success(balance);
        }, ct);
    }

    /// <summary>Issues a new access code, invalidating the old one and any signed-in sessions. Returns the plaintext once.</summary>
    public async Task<ServiceResult<string>> ResetAccessCodeAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id, ct)) return ServiceResult<string>.Fail("Customer not found.");

        var code = AccessCodes.Generate();
        var hash = codes.Hash(new Customer(), code);
        await db.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            await db.Customers.Where(c => c.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AccessCodeHash, hash)
                .SetProperty(c => c.AccessCodeVersion, c => c.AccessCodeVersion + 1)
                .SetProperty(c => c.FailedLoginCount, 0)
                .SetProperty(c => c.LockoutEndUtc, (DateTime?)null)
                .SetProperty(c => c.UpdatedAtUtc, now), token);
            audit.Record(actor, "customer.reset-code", "Customer", id.ToString());
            await db.SaveChangesAsync(token);
            return true;
        }, ct);
        return ServiceResult<string>.Success(code);
    }

    public async Task<ServiceResult> SetActiveAsync(int id, bool active, AdminActor actor, CancellationToken ct = default)
    {
        return await db.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var query = db.Customers.Where(c => c.Id == id);
            var updated = active
                ? await query.ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsActive, true)
                    .SetProperty(c => c.FailedLoginCount, 0)
                    .SetProperty(c => c.LockoutEndUtc, (DateTime?)null)
                    .SetProperty(c => c.UpdatedAtUtc, now), token)
                : await query.ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsActive, false)
                    // Ends existing sessions for good, so re-activating later doesn't revive them.
                    .SetProperty(c => c.AccessCodeVersion, c => c.AccessCodeVersion + 1)
                    .SetProperty(c => c.UpdatedAtUtc, now), token);
            if (updated == 0) return ServiceResult.Fail("Customer not found.");
            audit.Record(actor, active ? "customer.activate" : "customer.deactivate", "Customer", id.ToString());
            await db.SaveChangesAsync(token);
            return ServiceResult.Success;
        }, ct);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
