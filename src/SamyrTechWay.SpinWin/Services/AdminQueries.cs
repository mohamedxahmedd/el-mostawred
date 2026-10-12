using Microsoft.EntityFrameworkCore;
using SamyrTechWay.SpinWin.Data;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Models.ViewModels;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Read models for the admin area. All writes go through the command services.</summary>
public sealed class AdminQueries(AppDbContext db, SettingsService settings, TimeProvider clock)
{
    public const int PageSize = 25;

    public async Task<DashboardViewModel> DashboardAsync(CancellationToken ct)
    {
        var site = await settings.GetAsync(ct);
        var items = await db.WheelItems.AsNoTracking().Where(w => w.IsActive && !w.IsArchived).ToListAsync(ct);
        var odds = WheelOdds.Compute(items.Select(SpinService.ToSnapshot));

        var since = CairoTime.StartOfCairoDayUtc(clock.GetUtcNow().UtcDateTime).AddDays(-6);
        var recentForChart = await db.SpinAttempts.AsNoTracking().Where(a => a.CreatedAtUtc >= since)
            .Select(a => new { a.CreatedAtUtc, a.Kind }).ToListAsync(ct);
        var activity = Enumerable.Range(0, 7).Select(i =>
        {
            var dayStart = since.AddDays(i);
            var label = AdminText.Date(dayStart.AddHours(12), "ddd d");
            var inDay = recentForChart.Where(a => CairoTime.StartOfCairoDayUtc(a.CreatedAtUtc) == CairoTime.StartOfCairoDayUtc(dayStart.AddHours(12))).ToList();
            return new DayActivity(label, inDay.Count, inDay.Count(a => a.Kind == SpinResultKind.Won));
        }).ToList();

        return new DashboardViewModel
        {
            Customers = await db.Customers.CountAsync(ct),
            ActiveCustomers = await db.Customers.CountAsync(c => c.IsActive, ct),
            CustomersWithCredits = await db.Customers.CountAsync(c => c.IsActive && c.SpinCredits > 0, ct),
            AvailableCredits = await db.Customers.Where(c => c.IsActive).SumAsync(c => (int?)c.SpinCredits, ct) ?? 0,
            ConsumedCredits = await db.SpinAttempts.CountAsync(a => a.Kind == SpinResultKind.Won, ct),
            TotalAttempts = await db.SpinAttempts.CountAsync(ct),
            TryAgainAttempts = await db.SpinAttempts.CountAsync(a => a.Kind == SpinResultKind.TryAgain, ct),
            RealWins = await db.Awards.CountAsync(ct),
            PendingAwards = await db.Awards.CountAsync(a => a.Status == AwardStatus.Pending, ct),
            DeliveredAwards = await db.Awards.CountAsync(a => a.Status == AwardStatus.Delivered, ct),
            ActivePrizes = items.Count(i => i.Kind == WheelItemKind.Prize),
            CampaignEnabled = site.CampaignEnabled,
            WheelStatus = odds.Status,
            ConfiguredTotal = odds.ConfiguredTotal,
            RecentAttempts = (await db.SpinAttempts.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).Take(10)
                    .Select(a => new { a.Id, a.CreatedAtUtc, a.CustomerId, a.Customer!.FullName, a.Customer.NormalizedPhone, ItemName = (a.ItemNameAr != null && a.ItemNameAr != "" ? a.ItemNameAr : a.ItemNameEn), a.Kind })
                    .ToListAsync(ct))
                .Select(a => new RecentAttemptRow(a.Id, a.CreatedAtUtc, a.CustomerId, a.FullName, PhoneNumber.Mask(a.NormalizedPhone), a.ItemName, a.Kind))
                .ToList(),
            RecentPendingAwards = await db.Awards.AsNoTracking().Where(a => a.Status == AwardStatus.Pending)
                .OrderByDescending(a => a.CreatedAtUtc).Take(8)
                .Select(a => new PendingAwardRow(a.Id, a.CreatedAtUtc, a.CustomerId, a.Customer!.FullName, (a.PrizeNameAr != null && a.PrizeNameAr != "" ? a.PrizeNameAr : a.PrizeNameEn), a.CelebrationTier))
                .ToListAsync(ct),
            StockAlerts = items.Where(i => i.Kind == WheelItemKind.Prize && i.StockQuantity is not null && i.StockQuantity <= i.LowStockThreshold)
                .OrderBy(i => i.StockQuantity)
                .Select(i => new StockAlertRow(i.Id, AdminText.Name(i.NameEn, i.NameAr), i.StockQuantity!.Value, i.LowStockThreshold)).ToList(),
            Activity = activity,
        };
    }

    public async Task<CustomerListViewModel> CustomersAsync(CustomerListFilter f, CancellationToken ct)
    {
        f.Page = Math.Max(1, f.Page);
        var q = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            if (PhoneNumber.TryNormalize(term, out var phone))
            {
                q = q.Where(c => c.NormalizedPhone == phone);
            }
            else
            {
                var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
                var phoneFragment = digits.Length >= 4 ? (digits.StartsWith('0') ? digits[1..] : digits) : null;
                q = q.Where(c => c.FullName.Contains(term)
                                 || (c.InvoiceReference != null && c.InvoiceReference.Contains(term))
                                 || (phoneFragment != null && c.NormalizedPhone.Contains(phoneFragment)));
            }
        }
        q = f.Status switch
        {
            "active" => q.Where(c => c.IsActive),
            "disabled" => q.Where(c => !c.IsActive),
            _ => q,
        };
        if (f.WithCredits) q = q.Where(c => c.SpinCredits > 0);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id)
            .Skip((f.Page - 1) * PageSize).Take(PageSize)
            .Select(c => new
            {
                c.Id, c.FullName, c.NormalizedPhone, c.InvoiceReference, c.LaptopModel, c.SpinCredits, c.IsActive,
                c.CreatedAtUtc, c.LastLoginAtUtc, Awards = db.Awards.Count(a => a.CustomerId == c.Id),
            }).ToListAsync(ct);

        return new CustomerListViewModel
        {
            Filter = f,
            Result = new Paged<CustomerRow>(rows.Select(c => new CustomerRow(
                c.Id, c.FullName, PhoneNumber.ToLocalDisplay(c.NormalizedPhone), c.InvoiceReference, c.LaptopModel, c.SpinCredits,
                StateOf(c.IsActive, c.SpinCredits), c.CreatedAtUtc, c.LastLoginAtUtc, c.Awards)).ToList(), f.Page, PageSize, total),
        };
    }

    public static CustomerState StateOf(bool isActive, int credits) =>
        !isActive ? CustomerState.Disabled : credits > 0 ? CustomerState.Eligible : CustomerState.Finished;

    public async Task<CustomerDetailsViewModel?> CustomerAsync(int id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return null;
        var ledger = await db.CreditLedger.AsNoTracking().Where(l => l.CustomerId == id)
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id).ToListAsync(ct);
        return new CustomerDetailsViewModel
        {
            Customer = customer,
            State = StateOf(customer.IsActive, customer.SpinCredits),
            Ledger = ledger,
            LedgerReconciles = ledger.Sum(l => l.Delta) == customer.SpinCredits,
            Attempts = await db.SpinAttempts.AsNoTracking().Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.CreatedAtUtc).Take(200).ToListAsync(ct),
            Awards = await db.Awards.AsNoTracking().Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct),
        };
    }

    public async Task<Customer?> CustomerEntityAsync(int id, CancellationToken ct) =>
        await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<PrizesViewModel> PrizesAsync(CancellationToken ct)
    {
        var all = await db.WheelItems.AsNoTracking().OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).ToListAsync(ct);
        var wins = await db.Awards.AsNoTracking().GroupBy(a => a.WheelItemId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var odds = WheelOdds.Compute(all.Select(SpinService.ToSnapshot));
        PrizeRow Row(WheelItem i) => new(i, odds.ProbabilityOf(i.Id), odds.Eligible.Any(e => e.Item.Id == i.Id),
            i.Kind == WheelItemKind.Prize && i.StockQuantity is not null && i.StockQuantity <= i.LowStockThreshold,
            wins.GetValueOrDefault(i.Id));

        return new PrizesViewModel
        {
            Live = all.Where(i => !i.IsArchived).Select(Row).ToList(),
            Archived = all.Where(i => i.IsArchived).Select(Row).ToList(),
            Odds = odds,
            Preview = odds.Eligible.Count > 0 ? WheelPresenter.Items(all, odds, new Localizer("en")) : [],
        };
    }

    public async Task<WheelItem?> WheelItemAsync(int id, CancellationToken ct) =>
        await db.WheelItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<SpinListViewModel> SpinsAsync(SpinListFilter f, CancellationToken ct)
    {
        f.Page = Math.Max(1, f.Page);
        var q = db.SpinAttempts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            if (Guid.TryParse(term, out var guid))
                q = q.Where(a => a.Id == guid || a.RequestId == guid);
            else if (PhoneNumber.TryNormalize(term, out var phone))
                q = q.Where(a => a.Customer!.NormalizedPhone == phone);
            else
                q = q.Where(a => a.Customer!.FullName.Contains(term)
                                 || (a.Customer.InvoiceReference != null && a.Customer.InvoiceReference.Contains(term))
                                 || a.ItemNameEn.Contains(term) || (a.ItemNameAr != null && a.ItemNameAr.Contains(term)));
        }
        q = f.Kind switch
        {
            "won" => q.Where(a => a.Kind == SpinResultKind.Won),
            "tryagain" => q.Where(a => a.Kind == SpinResultKind.TryAgain),
            _ => q,
        };
        if (f.From is { } from) { var start = CairoTime.CairoDateToUtc(from); q = q.Where(a => a.CreatedAtUtc >= start); }
        if (f.To is { } to) { var end = CairoTime.CairoDateToUtc(to.AddDays(1)); q = q.Where(a => a.CreatedAtUtc < end); }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAtUtc)
            .Skip((f.Page - 1) * PageSize).Take(PageSize)
            .Select(a => new SpinRow(a.Id, a.CreatedAtUtc, a.CustomerId, a.Customer!.FullName, a.Customer.InvoiceReference,
                (a.ItemNameAr != null && a.ItemNameAr != "" ? a.ItemNameAr : a.ItemNameEn), a.Kind, a.CreditsBefore, a.CreditsAfter, a.RequestId, a.Award != null ? a.Award.Id : null))
            .ToListAsync(ct);
        return new SpinListViewModel { Filter = f, Result = new Paged<SpinRow>(rows, f.Page, PageSize, total) };
    }

    public async Task<SpinDetailsViewModel?> SpinAsync(Guid id, CancellationToken ct)
    {
        var attempt = await db.SpinAttempts.AsNoTracking().Include(a => a.Customer).Include(a => a.Award)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        return attempt?.Customer is null ? null
            : new SpinDetailsViewModel { Attempt = attempt, Customer = attempt.Customer, Award = attempt.Award };
    }

    public async Task<AwardListViewModel> AwardsAsync(AwardListFilter f, CancellationToken ct)
    {
        f.Page = Math.Max(1, f.Page);
        var q = db.Awards.AsNoTracking();
        q = f.Status switch
        {
            "pending" => q.Where(a => a.Status == AwardStatus.Pending),
            "delivered" => q.Where(a => a.Status == AwardStatus.Delivered),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            q = PhoneNumber.TryNormalize(term, out var phone)
                ? q.Where(a => a.Customer!.NormalizedPhone == phone)
                : q.Where(a => a.Customer!.FullName.Contains(term) || a.PrizeNameEn.Contains(term) || (a.PrizeNameAr != null && a.PrizeNameAr.Contains(term))
                               || (a.Customer.InvoiceReference != null && a.Customer.InvoiceReference.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(a => a.Status).ThenByDescending(a => a.CreatedAtUtc)
            .Skip((f.Page - 1) * PageSize).Take(PageSize)
            .Select(a => new
            {
                a.Id, a.CreatedAtUtc, a.CustomerId, a.Customer!.FullName, a.Customer.NormalizedPhone, a.Customer.InvoiceReference,
                PrizeName = (a.PrizeNameAr != null && a.PrizeNameAr != "" ? a.PrizeNameAr : a.PrizeNameEn), a.CelebrationTier, a.Status, a.DeliveredAtUtc, a.DeliveredByAdminName, a.AdminNote, a.SpinAttemptId,
            }).ToListAsync(ct);
        return new AwardListViewModel
        {
            Filter = f,
            Result = new Paged<AwardRow>(rows.Select(a => new AwardRow(a.Id, a.CreatedAtUtc, a.CustomerId, a.FullName,
                PhoneNumber.Mask(a.NormalizedPhone), a.InvoiceReference, a.PrizeName, a.CelebrationTier, a.Status,
                a.DeliveredAtUtc, a.DeliveredByAdminName, a.AdminNote, a.SpinAttemptId)).ToList(), f.Page, PageSize, total),
        };
    }

    public async Task<Paged<AuditRow>> AuditAsync(int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        var total = await db.AdminAuditLogs.CountAsync(ct);
        var rows = await db.AdminAuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new AuditRow(a.CreatedAtUtc, a.AdminName, a.Action, a.TargetType, a.TargetId, a.Details))
            .ToListAsync(ct);
        return new Paged<AuditRow>(rows, page, PageSize, total);
    }

    public async Task<SiteSettings> SettingsEntityAsync(CancellationToken ct) =>
        await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SiteSettings.SingletonId, ct) ?? SettingsService.Defaults();
}
