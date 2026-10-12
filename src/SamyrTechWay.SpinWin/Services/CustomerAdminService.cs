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
    public const string CustomerNotFoundMessage = "العميل غير موجود.";
    public const string DuplicatePhoneMessage = "يوجد عميل آخر مسجّل بنفس رقم الموبايل.";

    public async Task<ServiceResult<CreatedCustomer>> CreateAsync(CustomerInput input, int initialCredits, AdminActor actor, CancellationToken ct = default)
    {
        if (!PhoneNumber.TryNormalize(input.Phone, out var phone))
            return ServiceResult<CreatedCustomer>.Fail("اكتب رقم موبايل مصري صحيح يبدأ بـ 010 أو 011 أو 012 أو 015.", nameof(input.Phone));
        if (initialCredits is < 0 or > MaxInitialCredits)
            return ServiceResult<CreatedCustomer>.Fail($"عدد اللفّات المبدئي يجب أن يكون بين 0 و{MaxInitialCredits}.", "InitialCredits");
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
                        Note = "رصيد شراء اللابتوب",
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
            return ServiceResult.Fail("اكتب رقم موبايل مصري صحيح يبدأ بـ 010 أو 011 أو 012 أو 015.", nameof(input.Phone));

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return ServiceResult.Fail(CustomerNotFoundMessage);
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
            return ServiceResult.Fail("تم تعديل هذا السجل من مستخدم آخر. حدّث الصفحة وحاول مرة أخرى.");
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
            return ServiceResult<int>.Fail($"اختر عددًا من 1 إلى {MaxCreditAdjustment} لفّة.", "Amount");
        if (reason is null || reason.Length < 3)
            return ServiceResult<int>.Fail("اكتب سببًا لكل تغيير في رصيد اللفّات.", "Reason");

        return await db.InTransactionAsync(async token =>
        {
            var updated = await db.Customers
                .Where(c => c.Id == id && c.SpinCredits + delta >= 0)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.SpinCredits, c => c.SpinCredits + delta), token);
            if (updated == 0)
            {
                var exists = await db.Customers.AnyAsync(c => c.Id == id, token);
                return ServiceResult<int>.Fail(exists ? "لا يمكن أن يقل رصيد اللفّات عن صفر." : CustomerNotFoundMessage, "Amount");
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

    /// <summary>
    /// Removes all of a customer's spin credits (they can't spin until credits are granted again). Ledgered and audited.
    /// Returns how many credits were removed (0 when the balance was already zero).
    /// </summary>
    public async Task<ServiceResult<int>> ClearCreditsAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        // A spin can land between reading the balance and clearing it; the guarded update then misses, so read again.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await db.InTransactionAsync(async token =>
            {
                var current = await db.Customers.Where(c => c.Id == id).Select(c => (int?)c.SpinCredits).FirstOrDefaultAsync(token);
                if (current is null) return ServiceResult<int>.Fail(CustomerNotFoundMessage);
                if (current == 0) return ServiceResult<int>.Success(0);

                // Guarded on the balance we read, so the ledger entry always matches what was removed.
                var updated = await db.Customers
                    .Where(c => c.Id == id && c.SpinCredits == current)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.SpinCredits, 0), token);
                if (updated == 0) return null;

                db.CreditLedger.Add(new CreditLedgerEntry
                {
                    CustomerId = id,
                    Delta = -current.Value,
                    BalanceAfter = 0,
                    Reason = CreditReason.AdminRevoke,
                    AdminUserId = actor.Id,
                    AdminName = actor.Name,
                    Note = "تصفير كل اللفّات بواسطة المدير",
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                });
                audit.Record(actor, "credits.clear", "Customer", id.ToString(), new { removed = current.Value });
                await db.SaveChangesAsync(token);
                return ServiceResult<int>.Success(current.Value);
            }, ct);
            if (result is not null) return result;
        }
        return ServiceResult<int>.Fail("العميل يلف الآن. انتظر لحظة وحاول مرة أخرى.");
    }

    /// <summary>Issues a new access code, invalidating the old one and any signed-in sessions. Returns the plaintext once.</summary>
    public async Task<ServiceResult<string>> ResetAccessCodeAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id, ct)) return ServiceResult<string>.Fail(CustomerNotFoundMessage);

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
            if (updated == 0) return ServiceResult.Fail(CustomerNotFoundMessage);
            audit.Record(actor, active ? "customer.activate" : "customer.deactivate", "Customer", id.ToString());
            await db.SaveChangesAsync(token);
            return ServiceResult.Success;
        }, ct);
    }

    public const string PendingAwardBlocksDeleteMessage =
        "لا يمكن حذف هذا العميل لأن لديه جائزة في انتظار التسليم. سلّم الجائزة أولًا ثم احذفه.";

    /// <summary>
    /// Permanently deletes a customer with their spins, awards and ledger (owner request). Refused while a won prize
    /// is still owed to them. The audit log keeps a row recording who was deleted.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        return await db.InTransactionAsync(async token =>
        {
            // Takes the customer row lock without changing anything (the transaction commits even on a refusal), so a
            // spin either finishes before this check or waits and then finds the customer gone.
            var locked = await db.Customers.Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAtUtc, c => c.UpdatedAtUtc), token);
            if (locked == 0) return ServiceResult.Fail(CustomerNotFoundMessage);

            if (await db.Awards.AnyAsync(a => a.CustomerId == id && a.Status == AwardStatus.Pending, token))
                return ServiceResult.Fail(PendingAwardBlocksDeleteMessage);

            var customer = await db.Customers.AsNoTracking().Where(c => c.Id == id)
                .Select(c => new { c.FullName, c.NormalizedPhone }).SingleAsync(token);
            var ledger = await db.CreditLedger.Where(l => l.CustomerId == id).ExecuteDeleteAsync(token);
            var awards = await db.Awards.Where(a => a.CustomerId == id).ExecuteDeleteAsync(token);
            var spins = await db.SpinAttempts.Where(a => a.CustomerId == id).ExecuteDeleteAsync(token);
            await db.Customers.Where(c => c.Id == id).ExecuteDeleteAsync(token);

            audit.Record(actor, "customer.delete", "Customer", id.ToString(), new
            {
                name = customer.FullName,
                phone = PhoneNumber.Mask(customer.NormalizedPhone),
                spins, awards, ledger,
            });
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
