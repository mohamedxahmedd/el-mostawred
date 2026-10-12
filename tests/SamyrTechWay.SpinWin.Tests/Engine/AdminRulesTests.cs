using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;
using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Engine;

/// <summary>Admin-side rules: allocation validation (spec §12 case 9), single Try Again, soft delete, duplicate phones.</summary>
public class AdminRulesTests : IAsyncLifetime
{
    private TestHost _host = null!;
    public async ValueTask InitializeAsync() => _host = await TestHost.SqliteAsync();
    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<ServiceResult> SaveAllocationAsync(params AllocationRow[] rows) =>
        _host.RunAsync(sp => sp.GetRequiredService<WheelItemAdminService>().SaveAllocationAsync(rows, TestHost.Admin));

    [Fact]
    public async Task Allocation_must_total_exactly_100_percent()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        await _host.WithDbAsync(db => db.WheelItems.Where(w => w.IsArchived).ExecuteDeleteAsync()); // only the two live rows

        var under = await SaveAllocationAsync(new(ids["Pad"], true, 7000, 1), new(ids["Try Again"], true, 2500, 2));
        var over = await SaveAllocationAsync(new(ids["Pad"], true, 8000, 1), new(ids["Try Again"], true, 2500, 2));
        var ok = await SaveAllocationAsync(new(ids["Pad"], true, 6000, 1), new(ids["Try Again"], true, 4000, 2));

        Assert.False(under.Ok);
        Assert.False(over.Ok);
        Assert.True(ok.Ok, ok.Error);
        Assert.Equal(6000, (await _host.WithDbAsync(db => db.WheelItems.SingleAsync(w => w.Id == ids["Pad"]))).ProbabilityBasisPoints);
    }

    [Fact]
    public async Task Allocation_rejects_a_wheel_with_only_try_again_active()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        await _host.WithDbAsync(db => db.WheelItems.Where(w => w.IsArchived).ExecuteDeleteAsync());
        var result = await SaveAllocationAsync(new(ids["Pad"], false, 0, 1), new(ids["Try Again"], true, 10_000, 2));
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Editing_one_active_item_cannot_break_the_100_percent_total()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var item = await _host.WithDbAsync(db => db.WheelItems.AsNoTracking().SingleAsync(w => w.Id == ids["Pad"]));
        var input = new WheelItemInput("Pad", null, null, null, WheelItemKind.Prize, 0, 5000, "mousepad", "ruby", null, 3, true,
            CelebrationTier.Standard, null, null);
        var result = await _host.RunAsync(sp => sp.GetRequiredService<WheelItemAdminService>()
            .UpdateAsync(item.Id, input, null, false, item.RowVersion, TestHost.Admin));
        Assert.False(result.Ok);
        Assert.Equal(7500, (await _host.WithDbAsync(db => db.WheelItems.SingleAsync(w => w.Id == ids["Pad"]))).ProbabilityBasisPoints);
    }

    [Fact]
    public async Task Only_one_try_again_can_be_active()
    {
        await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var input = new WheelItemInput("Retry 2", null, null, null, WheelItemKind.TryAgain, 0, 0, "retry", "gold", null, 3, false,
            CelebrationTier.Standard, null, null);
        var created = await _host.RunAsync(sp => sp.GetRequiredService<WheelItemAdminService>().CreateAsync(input, null, TestHost.Admin));
        Assert.True(created.Ok); // inactive is fine

        // The database itself refuses a second active Try Again.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _host.WithDbAsync(async db =>
        {
            var second = await db.WheelItems.SingleAsync(w => w.Id == created.Value!.Id);
            second.IsActive = true;
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Active_items_cannot_be_archived_and_archiving_keeps_history()
    {
        var ids = await _host.SetWheelAsync(("Pad", WheelItemKind.Prize, 7500, null), ("Try Again", WheelItemKind.TryAgain, 2500, null));
        var customer = await _host.CreateCustomerAsync();
        _host.Random.Next = _ => 0;
        await _host.SpinAsync(customer);

        var svc = (Func<IServiceProvider, WheelItemAdminService>)(sp => sp.GetRequiredService<WheelItemAdminService>());
        Assert.False((await _host.RunAsync(sp => svc(sp).ArchiveAsync(ids["Pad"], TestHost.Admin))).Ok);

        await _host.WithDbAsync(db => db.WheelItems.Where(w => w.Id == ids["Pad"]).ExecuteUpdateAsync(s => s.SetProperty(w => w.IsActive, false)));
        Assert.True((await _host.RunAsync(sp => svc(sp).ArchiveAsync(ids["Pad"], TestHost.Admin))).Ok);
        Assert.Equal(1, await _host.WithDbAsync(db => db.Awards.CountAsync(a => a.WheelItemId == ids["Pad"])));
        Assert.True((await _host.WithDbAsync(db => db.WheelItems.SingleAsync(w => w.Id == ids["Pad"]))).IsArchived);
    }

    [Fact]
    public async Task Duplicate_phone_in_any_format_is_a_friendly_error()
    {
        await _host.CreateCustomerAsync(phone: "01012345678");
        var result = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>()
            .CreateAsync(new CustomerInput("Other", "+20 101 234 5678", null, null, null), 1, TestHost.Admin));
        Assert.False(result.Ok);
        Assert.Equal(CustomerAdminService.DuplicatePhoneMessage, result.Error);
    }

    [Fact]
    public async Task Audit_entries_never_contain_access_codes_or_hashes()
    {
        var id = await _host.CreateCustomerAsync();
        var code = await _host.RunAsync(sp => sp.GetRequiredService<CustomerAdminService>().ResetAccessCodeAsync(id, TestHost.Admin));
        var hash = (await _host.CustomerAsync(id)).AccessCodeHash;
        var details = await _host.WithDbAsync(db => db.AdminAuditLogs.Select(a => a.Details ?? "").ToListAsync());
        Assert.All(details, d =>
        {
            Assert.DoesNotContain(code.Value!, d);
            Assert.DoesNotContain(hash, d);
        });
    }
}
