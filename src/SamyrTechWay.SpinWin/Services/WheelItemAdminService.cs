using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

public sealed record WheelItemInput(
    string NameEn,
    string? NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    WheelItemKind Kind,
    int DisplayOrder,
    int BasisPoints,
    string? IconKey,
    string ColorKey,
    int? StockQuantity,
    int LowStockThreshold,
    bool IsActive,
    CelebrationTier Tier,
    string? CollectionNoteEn,
    string? CollectionNoteAr);

public sealed record NewImage(byte[] Data, string ContentType);

public sealed record AllocationRow(int Id, bool IsActive, int BasisPoints, int DisplayOrder);

public sealed class WheelItemAdminService(AppDbContext db, AuditService audit, TimeProvider clock)
{
    public static readonly string[] ColorKeys = ["ruby", "flame", "ember", "amber", "gold", "onyx"];

    public const string RebalanceHint =
        "Active wheel items must total exactly 100.00%. Use the Probability allocation table on the Prizes page to change weights or activation together.";

    public async Task<ServiceResult<WheelItem>> CreateAsync(WheelItemInput input, NewImage? image, AdminActor actor, CancellationToken ct = default)
    {
        var error = Validate(input);
        if (error is not null) return ServiceResult<WheelItem>.Fail(error.Value.Message, error.Value.Field);

        var all = await db.WheelItems.AsNoTracking().ToListAsync(ct);
        if (input.IsActive)
        {
            var total = all.Where(i => i.IsActive && !i.IsArchived).Sum(i => i.ProbabilityBasisPoints) + input.BasisPoints;
            if (total != WheelOdds.FullAllocation)
                return ServiceResult<WheelItem>.Fail($"Activating this item would make the active total {Percent(total)}. {RebalanceHint}", "IsActive");
            if (input.Kind == WheelItemKind.TryAgain && all.Any(i => i.IsActive && !i.IsArchived && i.Kind == WheelItemKind.TryAgain))
                return ServiceResult<WheelItem>.Fail("Only one Try Again item can be active.", "IsActive");
        }

        // Item and its audit row commit together.
        var created = await db.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var item = new WheelItem { CreatedAtUtc = now };
            Apply(item, input, now);
            if (image is not null) item.ImageId = AddImage(image, now);

            db.WheelItems.Add(item);
            await db.SaveChangesAsync(token);
            audit.Record(actor, "prize.create", "WheelItem", item.Id.ToString(),
                new { item.NameEn, kind = item.Kind.ToString(), item.ProbabilityBasisPoints, item.StockQuantity, item.IsActive });
            await db.SaveChangesAsync(token);
            return item;
        }, ct);
        return ServiceResult<WheelItem>.Success(created);
    }

    public async Task<ServiceResult> UpdateAsync(int id, WheelItemInput input, NewImage? image, bool removeImage, byte[] rowVersion, AdminActor actor, CancellationToken ct = default)
    {
        var item = await db.WheelItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult.Fail("Wheel item not found.");
        if (item.IsArchived) return ServiceResult.Fail("Restore this item before editing it.");

        input = input with { Kind = item.Kind }; // kind is fixed after creation
        var error = Validate(input);
        if (error is not null) return ServiceResult.Fail(error.Value.Message, error.Value.Field);

        var others = await db.WheelItems.AsNoTracking().Where(i => i.Id != id && i.IsActive && !i.IsArchived).ToListAsync(ct);
        if (item.IsActive || input.IsActive)
        {
            var total = others.Sum(i => i.ProbabilityBasisPoints) + (input.IsActive ? input.BasisPoints : 0);
            if (total != WheelOdds.FullAllocation)
                return ServiceResult.Fail($"This change would make the active total {Percent(total)}. {RebalanceHint}", "BasisPoints");
        }
        if (input.IsActive && item.Kind == WheelItemKind.TryAgain && others.Any(i => i.Kind == WheelItemKind.TryAgain))
            return ServiceResult.Fail("Only one Try Again item can be active.", "IsActive");

        db.Entry(item).Property(i => i.RowVersion).OriginalValue = rowVersion;
        var now = clock.GetUtcNow().UtcDateTime;
        var before = new { item.NameEn, item.ProbabilityBasisPoints, item.StockQuantity, item.IsActive, tier = item.CelebrationTier.ToString() };
        Apply(item, input, now);
        if (image is not null) item.ImageId = AddImage(image, now);
        else if (removeImage) item.ImageId = null;

        audit.Record(actor, "prize.update", "WheelItem", id.ToString(), new
        {
            before,
            after = new { item.NameEn, item.ProbabilityBasisPoints, item.StockQuantity, item.IsActive, tier = item.CelebrationTier.ToString() },
            imageChanged = image is not null || removeImage,
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("This item changed since you opened it (for example, stock was reserved by a win). Reload and try again.");
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ServiceResult.Fail("Only one Try Again item can be active.", "IsActive");
        }
    }

    /// <summary>Saves weights, activation and order for all live items together. Active total must be exactly 10,000.</summary>
    public async Task<ServiceResult> SaveAllocationAsync(IReadOnlyList<AllocationRow> rows, AdminActor actor, CancellationToken ct = default)
    {
        if (rows.Count == 0) return ServiceResult.Fail("Nothing to save.");
        if (rows.Select(r => r.Id).Distinct().Count() != rows.Count) return ServiceResult.Fail("Duplicate rows submitted.");
        if (rows.Any(r => r.BasisPoints is < 0 or > WheelOdds.FullAllocation))
            return ServiceResult.Fail("Each weight must be between 0.00% and 100.00%.");
        if (rows.Any(r => r.IsActive && r.BasisPoints <= 0))
            return ServiceResult.Fail("Active items need a weight above 0.00%. Deactivate items you want off the wheel.");

        var total = rows.Where(r => r.IsActive).Sum(r => r.BasisPoints);
        if (total != WheelOdds.FullAllocation)
            return ServiceResult.Fail($"Active weights total {Percent(total)}; they must total exactly 100.00%.");

        try
        {
            return await db.InTransactionAsync(async token =>
            {
                var ids = rows.Select(r => r.Id).ToList();
                var items = await db.WheelItems.Where(i => !i.IsArchived).ToListAsync(token);
                if (items.Count != rows.Count || items.Any(i => !ids.Contains(i.Id)))
                    return ServiceResult.Fail("The prize list changed while you were editing. Reload and try again.");

                var byId = rows.ToDictionary(r => r.Id);
                if (items.Count(i => i.Kind == WheelItemKind.TryAgain && byId[i.Id].IsActive) > 1)
                    return ServiceResult.Fail("Only one Try Again item can be active.");
                if (!items.Any(i => i.Kind == WheelItemKind.Prize && byId[i.Id].IsActive))
                    return ServiceResult.Fail("At least one real prize must be active.");

                var now = clock.GetUtcNow().UtcDateTime;
                var changes = new List<object>();
                var changed = items.Where(i =>
                {
                    var row = byId[i.Id];
                    return i.IsActive != row.IsActive || i.ProbabilityBasisPoints != row.BasisPoints || i.DisplayOrder != row.DisplayOrder;
                }).ToList();
                if (changed.Count == 0) return ServiceResult.Success;

                // Two passes: deactivations are saved before activations so the filtered unique index
                // (single active Try Again) is never transiently violated inside the batch.
                foreach (var pass in new[] { false, true })
                {
                    foreach (var item in changed.Where(i => byId[i.Id].IsActive == pass))
                    {
                        var row = byId[item.Id];
                        changes.Add(new { item.Id, from = item.ProbabilityBasisPoints, to = row.BasisPoints, active = row.IsActive, order = row.DisplayOrder });
                        item.IsActive = row.IsActive;
                        item.ProbabilityBasisPoints = row.BasisPoints;
                        item.DisplayOrder = row.DisplayOrder;
                        item.UpdatedAtUtc = now;
                    }
                    await db.SaveChangesAsync(token);
                }

                audit.Record(actor, "prize.allocation", "WheelItem", null, changes);
                await db.SaveChangesAsync(token);
                return ServiceResult.Success;
            }, ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ServiceResult.Fail("Only one Try Again item can be active.");
        }
    }

    /// <summary>Soft delete. Referenced history stays intact. Active items must be deactivated (rebalanced) first.</summary>
    public async Task<ServiceResult> ArchiveAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        var item = await db.WheelItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult.Fail("Wheel item not found.");
        if (item.IsArchived) return ServiceResult.Success;
        if (item.IsActive) return ServiceResult.Fail("Deactivate this item in the allocation table before archiving it.");
        item.IsArchived = true;
        item.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        audit.Record(actor, "prize.archive", "WheelItem", id.ToString(), new { item.NameEn });
        await db.SaveChangesAsync(ct);
        return ServiceResult.Success;
    }

    public async Task<ServiceResult> RestoreAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        var item = await db.WheelItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult.Fail("Wheel item not found.");
        if (!item.IsArchived) return ServiceResult.Success;
        item.IsArchived = false;
        item.IsActive = false;
        item.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        audit.Record(actor, "prize.restore", "WheelItem", id.ToString(), new { item.NameEn });
        await db.SaveChangesAsync(ct);
        return ServiceResult.Success;
    }

    private Guid AddImage(NewImage image, DateTime now)
    {
        var entity = new PrizeImage
        {
            Id = Guid.CreateVersion7(),
            ContentType = image.ContentType,
            Data = image.Data,
            ByteLength = image.Data.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(image.Data)),
            CreatedAtUtc = now,
        };
        db.PrizeImages.Add(entity);
        return entity.Id;
    }

    private static void Apply(WheelItem item, WheelItemInput input, DateTime now)
    {
        var retry = input.Kind == WheelItemKind.TryAgain;
        item.NameEn = input.NameEn.Trim();
        item.NameAr = Clean(input.NameAr);
        item.DescriptionEn = Clean(input.DescriptionEn);
        item.DescriptionAr = Clean(input.DescriptionAr);
        item.Kind = input.Kind;
        item.DisplayOrder = input.DisplayOrder;
        item.ProbabilityBasisPoints = input.BasisPoints;
        item.IconKey = Clean(input.IconKey);
        item.ColorKey = input.ColorKey;
        item.StockQuantity = retry ? null : input.StockQuantity;
        item.LowStockThreshold = input.LowStockThreshold;
        item.IsActive = input.IsActive;
        item.CelebrationTier = retry ? CelebrationTier.Standard : input.Tier;
        item.CollectionNoteEn = retry ? null : Clean(input.CollectionNoteEn);
        item.CollectionNoteAr = retry ? null : Clean(input.CollectionNoteAr);
        item.UpdatedAtUtc = now;
    }

    private static (string Message, string Field)? Validate(WheelItemInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NameEn)) return ("English name is required.", "NameEn");
        if (!Enum.IsDefined(input.Kind)) return ("Choose an item type.", "Kind");
        if (!Enum.IsDefined(input.Tier)) return ("Choose a celebration tier.", "Tier");
        if (input.BasisPoints is < 0 or > WheelOdds.FullAllocation) return ("Weight must be between 0.00% and 100.00%.", "BasisPoints");
        if (input.IsActive && input.BasisPoints == 0) return ("Active items need a weight above 0.00%.", "BasisPoints");
        if (input.StockQuantity is < 0) return ("Stock cannot be negative. Leave it empty for unlimited.", "StockQuantity");
        if (input.LowStockThreshold is < 0 or > 1000) return ("Low-stock alert must be between 0 and 1000.", "LowStockThreshold");
        if (input.IconKey is not null && !PrizeIcons.IsValid(input.IconKey)) return ("Choose an icon from the list.", "IconKey");
        if (!ColorKeys.Contains(input.ColorKey)) return ("Choose a wedge colour from the list.", "ColorKey");
        if (input.DisplayOrder is < 0 or > 999) return ("Display order must be between 0 and 999.", "DisplayOrder");
        return null;
    }

    public static string Percent(int basisPoints) => (basisPoints / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
