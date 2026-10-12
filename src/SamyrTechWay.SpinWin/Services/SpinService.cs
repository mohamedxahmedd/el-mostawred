using System.Data;
using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public enum SpinFailure
{
    None,
    InvalidRequest,
    CustomerNotFound,
    CustomerInactive,
    NoCredits,
    CampaignPaused,
    WheelUnavailable,
}

public sealed record SpinOutcome(
    SpinFailure Failure,
    SpinAttempt? Attempt = null,
    Award? Award = null,
    int Credits = 0,
    bool Replayed = false,
    IReadOnlyList<int>? DrawLayout = null)
{
    public bool Succeeded => Failure == SpinFailure.None && Attempt is not null;
    public static SpinOutcome Fail(SpinFailure failure, int credits = 0) => new(failure, Credits: credits);
}

/// <summary>
/// Server-authoritative spin. One SQL transaction: guarded credit debit (row lock serialises a customer),
/// CSPRNG weighted draw, guarded stock reservation, immutable attempt + ledger, Try Again refund or award.
/// Idempotent per (customer, requestId).
/// </summary>
public sealed class SpinService(
    AppDbContext db,
    SettingsService settings,
    IRandomSource random,
    TimeProvider clock,
    ILogger<SpinService> logger)
{
    private const int MaxStockRaceRedraws = 16;

    public async Task<SpinOutcome> SpinAsync(int customerId, Guid requestId, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty) return SpinOutcome.Fail(SpinFailure.InvalidRequest);

        // Fast idempotent path: a replayed key returns the stored result, nothing else happens.
        var replay = await FindReplayAsync(customerId, requestId, ct);
        if (replay is not null) return replay;

        var refusal = await PreflightAsync(customerId, ct);
        if (refusal is not null)
        {
            // A duplicate of this key may have committed since the check above (that commit is what used the credit):
            // its stored result wins over the refusal.
            return await FindReplayAsync(customerId, requestId, ct) ?? refusal;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(ct2 => SpinInTransactionAsync(customerId, requestId, ct2), ct);
    }

    /// <summary>Refusals that need no transaction: unknown/inactive customer, paused campaign, no credits, unusable wheel.</summary>
    private async Task<SpinOutcome?> PreflightAsync(int customerId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new { c.IsActive, c.SpinCredits })
            .FirstOrDefaultAsync(ct);
        if (customer is null) return SpinOutcome.Fail(SpinFailure.CustomerNotFound);
        if (!customer.IsActive) return SpinOutcome.Fail(SpinFailure.CustomerInactive, customer.SpinCredits);

        var site = await settings.GetAsync(ct);
        if (!site.CampaignEnabled) return SpinOutcome.Fail(SpinFailure.CampaignPaused, customer.SpinCredits);
        if (customer.SpinCredits <= 0) return SpinOutcome.Fail(SpinFailure.NoCredits);

        // Block before touching credits when the wheel cannot produce a real prize.
        if (!WheelOdds.Compute(await LoadSnapshotsAsync(ct)).CanSpin)
        {
            logger.LogWarning("Spin blocked for customer {CustomerId}: wheel configuration has no eligible prize or invalid allocation", customerId);
            return SpinOutcome.Fail(SpinFailure.WheelUnavailable, customer.SpinCredits);
        }
        return null;
    }

    private async Task<SpinOutcome> SpinInTransactionAsync(int customerId, Guid requestId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var now = clock.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // 1. Guarded atomic debit. Takes the customer row lock until commit.
            var debited = await db.Customers
                .Where(c => c.Id == customerId && c.IsActive && c.SpinCredits > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.SpinCredits, c => c.SpinCredits - 1), ct);

            if (debited == 0)
            {
                await tx.RollbackAsync(ct);
                return await FindReplayAsync(customerId, requestId, ct) ?? await DescribeRefusalAsync(customerId, ct);
            }

            // 2. Re-check idempotency under the lock (a concurrent duplicate may have just committed).
            if (await db.SpinAttempts.AnyAsync(a => a.CustomerId == customerId && a.RequestId == requestId, ct))
            {
                await tx.RollbackAsync(ct);
                return await FindReplayAsync(customerId, requestId, ct)
                       ?? throw new InvalidOperationException("Attempt vanished during replay.");
            }

            var creditsAfterDebit = await db.Customers.Where(c => c.Id == customerId).Select(c => c.SpinCredits).SingleAsync(ct);
            var creditsBefore = creditsAfterDebit + 1;

            // 3. Draw. Stock is reserved with a guarded decrement; losing a race for the last unit redraws without it.
            var items = await db.WheelItems.AsNoTracking().Where(w => w.IsActive && !w.IsArchived).ToListAsync(ct);
            var snapshots = items.Select(ToSnapshot).ToList();
            var excluded = new HashSet<int>();
            IReadOnlyList<int>? layout = null;

            for (var round = 0; round < MaxStockRaceRedraws; round++)
            {
                var odds = WheelOdds.Compute(snapshots, excluded);
                if (!odds.CanSpin)
                {
                    await tx.RollbackAsync(ct);
                    logger.LogWarning("Spin for customer {CustomerId} rolled back: no eligible prize remained after stock check", customerId);
                    return SpinOutcome.Fail(SpinFailure.WheelUnavailable, creditsBefore);
                }

                layout ??= odds.Eligible.Select(e => e.Item.Id).ToList();
                var pick = odds.Draw(random.NextInt);

                if (pick.Item.Kind == WheelItemKind.Prize && pick.Item.StockQuantity is not null)
                {
                    var reserved = await db.WheelItems
                        .Where(w => w.Id == pick.Item.Id && w.IsActive && !w.IsArchived && w.StockQuantity > 0)
                        .ExecuteUpdateAsync(s => s.SetProperty(w => w.StockQuantity, w => w.StockQuantity - 1), ct);
                    if (reserved == 0)
                    {
                        excluded.Add(pick.Item.Id);
                        continue;
                    }
                }

                var item = items.Single(i => i.Id == pick.Item.Id);
                var isRetry = item.Kind == WheelItemKind.TryAgain;

                var attempt = new SpinAttempt
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = customerId,
                    WheelItemId = item.Id,
                    RequestId = requestId,
                    Kind = isRetry ? SpinResultKind.TryAgain : SpinResultKind.Won,
                    ItemNameEn = item.NameEn,
                    ItemNameAr = item.NameAr,
                    ItemKind = item.Kind,
                    ImageId = item.ImageId,
                    IconKey = item.IconKey,
                    ColorKey = item.ColorKey,
                    CelebrationTier = item.CelebrationTier,
                    ConfiguredBasisPoints = item.ProbabilityBasisPoints,
                    EffectiveWeight = pick.Weight,
                    EffectiveTotalWeight = odds.EffectiveTotal,
                    CreditsBefore = creditsBefore,
                    CreditsAfter = isRetry ? creditsBefore : creditsAfterDebit,
                    CreatedAtUtc = now,
                };
                db.SpinAttempts.Add(attempt);
                db.CreditLedger.Add(new CreditLedgerEntry
                {
                    CustomerId = customerId,
                    Delta = -1,
                    BalanceAfter = creditsAfterDebit,
                    Reason = CreditReason.SpinDebit,
                    SpinAttemptId = attempt.Id,
                    CreatedAtUtc = now,
                });

                Award? award = null;
                if (isRetry)
                {
                    // 4a. Try Again: refund in the same transaction.
                    await db.Customers.Where(c => c.Id == customerId)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.SpinCredits, c => c.SpinCredits + 1), ct);
                    db.CreditLedger.Add(new CreditLedgerEntry
                    {
                        CustomerId = customerId,
                        Delta = 1,
                        BalanceAfter = creditsBefore,
                        Reason = CreditReason.TryAgainRefund,
                        SpinAttemptId = attempt.Id,
                        Note = "استرداد تلقائي (حاول مرة أخرى)",
                        CreatedAtUtc = now,
                    });
                }
                else
                {
                    // 4b. Real prize: pending award with an immutable snapshot.
                    award = new Award
                    {
                        SpinAttemptId = attempt.Id,
                        CustomerId = customerId,
                        WheelItemId = item.Id,
                        PrizeNameEn = item.NameEn,
                        PrizeNameAr = item.NameAr,
                        ImageId = item.ImageId,
                        IconKey = item.IconKey,
                        ColorKey = item.ColorKey,
                        CelebrationTier = item.CelebrationTier,
                        CollectionNoteEn = item.CollectionNoteEn,
                        CollectionNoteAr = item.CollectionNoteAr,
                        Status = AwardStatus.Pending,
                        CreatedAtUtc = now,
                    };
                    db.Awards.Add(award);
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                logger.LogInformation("Spin {AttemptId} for customer {CustomerId}: {Kind} item {ItemId}",
                    attempt.Id, customerId, attempt.Kind, item.Id);
                return new SpinOutcome(SpinFailure.None, attempt, award, attempt.CreditsAfter, Replayed: false, layout);
            }

            await tx.RollbackAsync(ct);
            logger.LogWarning("Spin for customer {CustomerId} rolled back after repeated stock races", customerId);
            return SpinOutcome.Fail(SpinFailure.WheelUnavailable, creditsBefore);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await FindReplayAsync(customerId, requestId, ct) ?? throw new InvalidOperationException("Duplicate spin without stored attempt.", ex);
        }
    }

    private async Task<SpinOutcome> DescribeRefusalAsync(int customerId, CancellationToken ct)
    {
        var state = await db.Customers.AsNoTracking().Where(c => c.Id == customerId)
            .Select(c => new { c.IsActive, c.SpinCredits }).FirstOrDefaultAsync(ct);
        if (state is null) return SpinOutcome.Fail(SpinFailure.CustomerNotFound);
        return state.IsActive
            ? SpinOutcome.Fail(SpinFailure.NoCredits, state.SpinCredits)
            : SpinOutcome.Fail(SpinFailure.CustomerInactive, state.SpinCredits);
    }

    /// <summary>Stored attempt for this key, plus the customer's current credits. Null when the key is unused.</summary>
    public async Task<SpinOutcome?> FindReplayAsync(int customerId, Guid requestId, CancellationToken ct = default)
    {
        var attempt = await db.SpinAttempts.AsNoTracking()
            .Include(a => a.Award)
            .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.RequestId == requestId, ct);
        if (attempt is null) return null;
        var credits = await db.Customers.Where(c => c.Id == customerId).Select(c => c.SpinCredits).SingleAsync(ct);
        return new SpinOutcome(SpinFailure.None, attempt, attempt.Award, credits, Replayed: true);
    }

    public async Task<List<WheelItemSnapshot>> LoadSnapshotsAsync(CancellationToken ct = default) =>
        (await db.WheelItems.AsNoTracking().Where(w => w.IsActive && !w.IsArchived).ToListAsync(ct))
        .Select(ToSnapshot).ToList();

    public static WheelItemSnapshot ToSnapshot(WheelItem w) =>
        new(w.Id, w.Kind, w.ProbabilityBasisPoints, w.StockQuantity, w.IsActive, w.IsArchived, w.DisplayOrder);
}
