using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public enum CustomerLoginStatus
{
    Success,
    /// <summary>Wrong phone, wrong code, or temporarily locked. Deliberately indistinguishable to the caller.</summary>
    Rejected,
    Inactive,
}

public sealed record CustomerLoginResult(CustomerLoginStatus Status, Customer? Customer = null);

public sealed class CustomerAuthService(AppDbContext db, AccessCodes codes, TimeProvider clock, ILogger<CustomerAuthService> logger)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<CustomerLoginResult> LoginAsync(string? phoneInput, string? codeInput, CancellationToken ct = default)
    {
        var code = AccessCodes.Normalize(codeInput);
        if (!PhoneNumber.TryNormalize(phoneInput, out var phone) || code is null)
        {
            codes.VerifyDummy(code ?? "AAAAAAAAAA");
            return new(CustomerLoginStatus.Rejected);
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.NormalizedPhone == phone, ct);
        if (customer is null)
        {
            codes.VerifyDummy(code);
            return new(CustomerLoginStatus.Rejected);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (customer.LockoutEndUtc > now)
        {
            codes.VerifyDummy(code);
            logger.LogWarning("Customer {CustomerId} sign-in refused: locked out until {Until:u}", customer.Id, customer.LockoutEndUtc);
            return new(CustomerLoginStatus.Rejected);
        }

        if (!codes.Verify(customer, code))
        {
            // One atomic UPDATE: every SET reads the row's current count, so concurrent wrong guesses can't
            // all see a stale count and slip past the limit. The 5th failure locks and resets the counter.
            DateTime? lockUntil = now.Add(LockoutDuration);
            await db.Customers.Where(c => c.Id == customer.Id).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LockoutEndUtc, c => c.FailedLoginCount + 1 >= MaxFailedAttempts ? lockUntil : c.LockoutEndUtc)
                .SetProperty(c => c.FailedLoginCount, c => c.FailedLoginCount + 1 >= MaxFailedAttempts ? 0 : c.FailedLoginCount + 1), ct);
            if (await db.Customers.AnyAsync(c => c.Id == customer.Id && c.LockoutEndUtc == lockUntil, ct))
                logger.LogWarning("Customer {CustomerId} locked out after {Failures} failed access-code attempts", customer.Id, MaxFailedAttempts);
            return new(CustomerLoginStatus.Rejected);
        }

        if (!customer.IsActive) return new(CustomerLoginStatus.Inactive, customer);

        await db.Customers.Where(c => c.Id == customer.Id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.FailedLoginCount, 0)
            .SetProperty(c => c.LockoutEndUtc, (DateTime?)null)
            .SetProperty(c => c.LastLoginAtUtc, now), ct);
        return new(CustomerLoginStatus.Success, customer);
    }

    /// <summary>Used by cookie validation: the session survives only while the customer is active and the code unchanged.</summary>
    public Task<bool> IsSessionValidAsync(int customerId, int accessCodeVersion, CancellationToken ct = default) =>
        db.Customers.AnyAsync(c => c.Id == customerId && c.IsActive && c.AccessCodeVersion == accessCodeVersion, ct);
}
