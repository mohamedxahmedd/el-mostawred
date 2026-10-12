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

    /// <summary>Form field that weight errors belong to (the admin form edits weights as a percentage).</summary>
    public const string WeightField = "WeightPercent";

    public const string RebalanceHint =
        "مجموع نسب العناصر المفعّلة على العجلة يجب أن يكون 100.00% بالضبط. استخدم جدول توزيع النسب في صفحة الجوائز لتغيير النسب أو التفعيل معًا.";

    public async Task<ServiceResult<WheelItem>> CreateAsync(WheelItemInput input, NewImage? image, AdminActor actor, CancellationToken ct = default)
    {
        var error = Validate(input);
        if (error is not null) return ServiceResult<WheelItem>.Fail(error.Value.Message, error.Value.Field);

        var all = await db.WheelItems.AsNoTracking().ToListAsync(ct);
        if (input.IsActive)
        {
            var total = all.Where(i => i.IsActive && !i.IsArchived).Sum(i => i.ProbabilityBasisPoints) + input.BasisPoints;
            if (total != WheelOdds.FullAllocation)
                return ServiceResult<WheelItem>.Fail($"تفعيل هذا العنصر سيجعل مجموع النسب المفعّلة {Percent(total)}. {RebalanceHint}", "IsActive");
            if (input.Kind == WheelItemKind.TryAgain && all.Any(i => i.IsActive && !i.IsArchived && i.Kind == WheelItemKind.TryAgain))
                return ServiceResult<WheelItem>.Fail("يمكن تفعيل عنصر واحد فقط من نوع «حاول مرة أخرى».", "IsActive");
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

    /// <summary>
    /// Saves one item. When the change would move the active total off 100%, the active Try Again slice absorbs the
    /// difference (it must stay above 0%). Returns Try Again's new weight in basis points when it was adjusted, else null.
    /// </summary>
    public async Task<ServiceResult<int?>> UpdateAsync(int id, WheelItemInput input, NewImage? image, bool removeImage, byte[] rowVersion, AdminActor actor, CancellationToken ct = default)
    {
        var item = await db.WheelItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult<int?>.Fail("العنصر غير موجود.");
        if (item.IsArchived) return ServiceResult<int?>.Fail("استعد هذا العنصر أولًا قبل تعديله.");

        input = input with { Kind = item.Kind }; // kind is fixed after creation
        var error = Validate(input);
        if (error is not null) return ServiceResult<int?>.Fail(error.Value.Message, error.Value.Field);

        var others = await db.WheelItems.AsNoTracking().Where(i => i.Id != id && i.IsActive && !i.IsArchived).ToListAsync(ct);
        if (input.IsActive && item.Kind == WheelItemKind.TryAgain && others.Any(i => i.Kind == WheelItemKind.TryAgain))
            return ServiceResult<int?>.Fail("يمكن تفعيل عنصر واحد فقط من نوع «حاول مرة أخرى».", "IsActive");

        var now = clock.GetUtcNow().UtcDateTime;
        WheelItem? retry = null;
        int? retryBefore = null;
        if (item.IsActive || input.IsActive)
        {
            var total = others.Sum(i => i.ProbabilityBasisPoints) + (input.IsActive ? input.BasisPoints : 0);
            if (total != WheelOdds.FullAllocation)
            {
                if (item.Kind == WheelItemKind.TryAgain)
                    return ServiceResult<int?>.Fail($"هذا التغيير سيجعل مجموع النسب المفعّلة {Percent(total)}. لتغيير نسبة «حاول مرة أخرى»، استخدم جدول توزيع النسب في صفحة الجوائز.", WeightField);
                if (!input.IsActive && !others.Any(i => i.Kind == WheelItemKind.Prize))
                    return ServiceResult<int?>.Fail("يجب أن تبقى جائزة حقيقية واحدة على الأقل مفعّلة، وإلا لن يفوز أحد. فعّل جائزة أخرى أولًا.", "IsActive");

                var activeRetry = others.FirstOrDefault(i => i.Kind == WheelItemKind.TryAgain);
                var adjusted = activeRetry is null ? 0 : activeRetry.ProbabilityBasisPoints + (WheelOdds.FullAllocation - total);
                if (activeRetry is null || adjusted <= 0 || adjusted > WheelOdds.FullAllocation)
                {
                    var why = activeRetry is null
                        ? "لا توجد شريحة «حاول مرة أخرى» مفعّلة لتعويض الفرق."
                        : $"شريحة «حاول مرة أخرى» ({Percent(activeRetry.ProbabilityBasisPoints)}) لا تكفي لتعويض هذا الفرق.";
                    return ServiceResult<int?>.Fail($"هذا التغيير سيجعل مجموع النسب المفعّلة {Percent(total)}. {why} {RebalanceHint}", WeightField);
                }

                // The new weight is computed from the snapshot above, so the save must fail if Try Again changed since then.
                retry = await db.WheelItems.FirstAsync(i => i.Id == activeRetry.Id, ct);
                db.Entry(retry).Property(r => r.RowVersion).OriginalValue = activeRetry.RowVersion;
                retryBefore = activeRetry.ProbabilityBasisPoints;
                retry.ProbabilityBasisPoints = adjusted;
                retry.UpdatedAtUtc = now;
            }
        }

        db.Entry(item).Property(i => i.RowVersion).OriginalValue = rowVersion;
        var before = new { item.NameEn, item.ProbabilityBasisPoints, item.StockQuantity, item.IsActive, tier = item.CelebrationTier.ToString() };
        Apply(item, input, now);
        if (image is not null) item.ImageId = AddImage(image, now);
        else if (removeImage) item.ImageId = null;

        audit.Record(actor, "prize.update", "WheelItem", id.ToString(), new
        {
            before,
            after = new { item.NameEn, item.ProbabilityBasisPoints, item.StockQuantity, item.IsActive, tier = item.CelebrationTier.ToString() },
            imageChanged = image is not null || removeImage,
            tryAgainAdjusted = retry is null ? null : new { retry.Id, from = retryBefore, to = retry.ProbabilityBasisPoints },
        });
        if (retry is not null)
            audit.Record(actor, "prize.update", "WheelItem", retry.Id.ToString(), new
            {
                weight = new { from = retryBefore, to = retry.ProbabilityBasisPoints },
                reason = $"Balanced automatically when wheel item {id} was saved",
            });
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult<int?>.Success(retry?.ProbabilityBasisPoints);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<int?>.Fail(retry is null
                ? "تم تعديل هذا العنصر بعد أن فتحته (مثلًا حُجز من المخزون بسبب فوز). حدّث الصفحة وحاول مرة أخرى."
                : "تم تعديل هذا العنصر أو شريحة «حاول مرة أخرى» بعد أن فتحته (مثلًا حفظ شخص آخر جدول توزيع النسب). حدّث الصفحة وحاول مرة أخرى.");
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ServiceResult<int?>.Fail("يمكن تفعيل عنصر واحد فقط من نوع «حاول مرة أخرى».", "IsActive");
        }
    }

    /// <summary>Saves weights, activation and order for all live items together. Active total must be exactly 10,000.</summary>
    public async Task<ServiceResult> SaveAllocationAsync(IReadOnlyList<AllocationRow> rows, AdminActor actor, CancellationToken ct = default)
    {
        if (rows.Count == 0) return ServiceResult.Fail("لا يوجد شيء للحفظ.");
        if (rows.Select(r => r.Id).Distinct().Count() != rows.Count) return ServiceResult.Fail("تم إرسال صفوف مكررة.");
        if (rows.Any(r => r.BasisPoints is < 0 or > WheelOdds.FullAllocation))
            return ServiceResult.Fail("كل نسبة يجب أن تكون بين 0.00% و100.00%.");
        if (rows.Any(r => r.IsActive && r.BasisPoints <= 0))
            return ServiceResult.Fail("العناصر المفعّلة تحتاج نسبة أكبر من 0.00%. أوقف تفعيل العناصر التي تريد إخراجها من العجلة.");

        var total = rows.Where(r => r.IsActive).Sum(r => r.BasisPoints);
        if (total != WheelOdds.FullAllocation)
            return ServiceResult.Fail($"مجموع النسب المفعّلة {Percent(total)}، ويجب أن يكون 100.00% بالضبط.");

        try
        {
            return await db.InTransactionAsync(async token =>
            {
                var ids = rows.Select(r => r.Id).ToList();
                var items = await db.WheelItems.Where(i => !i.IsArchived).ToListAsync(token);
                if (items.Count != rows.Count || items.Any(i => !ids.Contains(i.Id)))
                    return ServiceResult.Fail("تغيّرت قائمة الجوائز أثناء التعديل. حدّث الصفحة وحاول مرة أخرى.");

                var byId = rows.ToDictionary(r => r.Id);
                if (items.Count(i => i.Kind == WheelItemKind.TryAgain && byId[i.Id].IsActive) > 1)
                    return ServiceResult.Fail("يمكن تفعيل عنصر واحد فقط من نوع «حاول مرة أخرى».");
                if (!items.Any(i => i.Kind == WheelItemKind.Prize && byId[i.Id].IsActive))
                    return ServiceResult.Fail("يجب تفعيل جائزة حقيقية واحدة على الأقل.");

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
            return ServiceResult.Fail("يمكن تفعيل عنصر واحد فقط من نوع «حاول مرة أخرى».");
        }
    }

    /// <summary>Soft delete. Referenced history stays intact. Active items must be deactivated (rebalanced) first.</summary>
    public async Task<ServiceResult> ArchiveAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        var item = await db.WheelItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult.Fail("العنصر غير موجود.");
        if (item.IsArchived) return ServiceResult.Success;
        if (item.IsActive) return ServiceResult.Fail("أوقف تفعيل هذا العنصر من جدول توزيع النسب قبل أرشفته.");
        item.IsArchived = true;
        item.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        audit.Record(actor, "prize.archive", "WheelItem", id.ToString(), new { item.NameEn });
        await db.SaveChangesAsync(ct);
        return ServiceResult.Success;
    }

    public async Task<ServiceResult> RestoreAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        var item = await db.WheelItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult.Fail("العنصر غير موجود.");
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
        if (string.IsNullOrWhiteSpace(input.NameEn)) return ("الاسم بالإنجليزية مطلوب.", "NameEn");
        if (!Enum.IsDefined(input.Kind)) return ("اختر نوع العنصر.", "Kind");
        if (!Enum.IsDefined(input.Tier)) return ("اختر مستوى الاحتفال.", "Tier");
        if (input.BasisPoints is < 0 or > WheelOdds.FullAllocation) return ("النسبة يجب أن تكون بين 0.00% و100.00%.", WeightField);
        if (input.IsActive && input.BasisPoints == 0) return ("العناصر المفعّلة تحتاج نسبة أكبر من 0.00%.", WeightField);
        if (input.StockQuantity is < 0) return ("المخزون لا يمكن أن يكون سالبًا. اتركه فارغًا ليكون غير محدود.", "StockQuantity");
        if (input.LowStockThreshold is < 0 or > 1000) return ("تنبيه انخفاض المخزون يجب أن يكون بين 0 و1000.", "LowStockThreshold");
        if (input.IconKey is not null && !PrizeIcons.IsValid(input.IconKey)) return ("اختر أيقونة من القائمة.", "IconKey");
        if (!ColorKeys.Contains(input.ColorKey)) return ("اختر لون الشريحة من القائمة.", "ColorKey");
        if (input.DisplayOrder is < 0 or > 999) return ("ترتيب العرض يجب أن يكون بين 0 و999.", "DisplayOrder");
        return null;
    }

    public static string Percent(int basisPoints) => (basisPoints / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
