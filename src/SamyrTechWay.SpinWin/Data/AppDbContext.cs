using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AdminUser>(options), IDataProtectionKeyContext
{
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<WheelItem> WheelItems => Set<WheelItem>();
    public DbSet<PrizeImage> PrizeImages => Set<PrizeImage>();
    public DbSet<SpinAttempt> SpinAttempts => Set<SpinAttempt>();
    public DbSet<Award> Awards => Set<Award>();
    public DbSet<CreditLedgerEntry> CreditLedger => Set<CreditLedgerEntry>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    private bool IsSqlite => Database.ProviderName == SqliteProvider;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // The model differs slightly per provider (rowversion emulation on SQLite), so cache it per provider.
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AdminUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(100);
        });

        b.Entity<Customer>(e =>
        {
            e.ToTable("Customers", t => t.HasCheckConstraint("CK_Customers_SpinCredits", "[SpinCredits] >= 0"));
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.NormalizedPhone).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.NormalizedPhone).IsUnique();
            e.Property(x => x.InvoiceReference).HasMaxLength(60);
            e.HasIndex(x => x.InvoiceReference);
            e.Property(x => x.LaptopModel).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.AccessCodeHash).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.CreatedAtUtc);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.FirstName);
        });

        b.Entity<PrizeImage>(e =>
        {
            e.Property(x => x.ContentType).HasMaxLength(40).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.Data).IsRequired();
        });

        b.Entity<WheelItem>(e =>
        {
            e.ToTable("WheelItems", t =>
            {
                t.HasCheckConstraint("CK_WheelItems_Probability", "[ProbabilityBasisPoints] >= 0 AND [ProbabilityBasisPoints] <= 10000");
                t.HasCheckConstraint("CK_WheelItems_Stock", "[StockQuantity] IS NULL OR [StockQuantity] >= 0");
            });
            e.Property(x => x.NameEn).HasMaxLength(80).IsRequired();
            e.Property(x => x.NameAr).HasMaxLength(80);
            e.Property(x => x.DescriptionEn).HasMaxLength(300);
            e.Property(x => x.DescriptionAr).HasMaxLength(300);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CelebrationTier).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IconKey).HasMaxLength(40);
            e.Property(x => x.ColorKey).HasMaxLength(20);
            e.Property(x => x.CollectionNoteEn).HasMaxLength(300);
            e.Property(x => x.CollectionNoteAr).HasMaxLength(300);
            e.HasOne(x => x.Image).WithMany().HasForeignKey(x => x.ImageId).OnDelete(DeleteBehavior.Restrict);
            // At most one live Try Again item.
            e.HasIndex(x => x.Kind)
                .HasDatabaseName("UX_WheelItems_SingleActiveTryAgain")
                .IsUnique()
                .HasFilter("[Kind] = 'TryAgain' AND [IsActive] = 1 AND [IsArchived] = 0");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.IsTryAgain);
        });

        b.Entity<SpinAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.CustomerId, x.RequestId }).IsUnique();
            e.HasIndex(x => x.CreatedAtUtc);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ItemKind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CelebrationTier).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ItemNameEn).HasMaxLength(80);
            e.Property(x => x.ItemNameAr).HasMaxLength(80);
            e.Property(x => x.IconKey).HasMaxLength(40);
            e.Property(x => x.ColorKey).HasMaxLength(20);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.WheelItem).WithMany().HasForeignKey(x => x.WheelItemId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Award>(e =>
        {
            e.HasIndex(x => x.SpinAttemptId).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            e.HasIndex(x => x.CustomerId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CelebrationTier).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PrizeNameEn).HasMaxLength(80);
            e.Property(x => x.PrizeNameAr).HasMaxLength(80);
            e.Property(x => x.IconKey).HasMaxLength(40);
            e.Property(x => x.ColorKey).HasMaxLength(20);
            e.Property(x => x.CollectionNoteEn).HasMaxLength(300);
            e.Property(x => x.CollectionNoteAr).HasMaxLength(300);
            e.Property(x => x.DeliveredByAdminId).HasMaxLength(450);
            e.Property(x => x.DeliveredByAdminName).HasMaxLength(100);
            e.Property(x => x.AdminNote).HasMaxLength(500);
            e.HasOne(x => x.SpinAttempt).WithOne(x => x.Award).HasForeignKey<Award>(x => x.SpinAttemptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WheelItem>().WithMany().HasForeignKey(x => x.WheelItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<CreditLedgerEntry>(e =>
        {
            e.ToTable("CreditLedgerEntries");
            e.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.AdminUserId).HasMaxLength(450);
            e.Property(x => x.AdminName).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SpinAttempt>().WithMany().HasForeignKey(x => x.SpinAttemptId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SiteSettings>(e =>
        {
            e.ToTable("SiteSettings", t =>
                t.HasCheckConstraint("CK_SiteSettings_Duration", "[SpinDurationMinMs] >= 1500 AND [SpinDurationMaxMs] <= 12000 AND [SpinDurationMinMs] <= [SpinDurationMaxMs]"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.SiteTitle).HasMaxLength(120);
            e.Property(x => x.PromoSubheadingEn).HasMaxLength(300);
            e.Property(x => x.PromoSubheadingAr).HasMaxLength(300);
            e.Property(x => x.PickupInstructionsEn).HasMaxLength(500);
            e.Property(x => x.PickupInstructionsAr).HasMaxLength(500);
            e.Property(x => x.AnnouncementEn).HasMaxLength(300);
            e.Property(x => x.AnnouncementAr).HasMaxLength(300);
            e.Property(x => x.PausedMessageEn).HasMaxLength(300);
            e.Property(x => x.PausedMessageAr).HasMaxLength(300);
            e.Property(x => x.DefaultLanguage).HasMaxLength(5);
            e.Property(x => x.AnimationIntensity).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<AdminAuditLog>(e =>
        {
            e.HasIndex(x => x.CreatedAtUtc);
            e.Property(x => x.AdminUserId).HasMaxLength(450);
            e.Property(x => x.AdminName).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(60);
            e.Property(x => x.TargetType).HasMaxLength(40);
            e.Property(x => x.TargetId).HasMaxLength(64);
            e.Property(x => x.Details).HasMaxLength(1000);
        });

        if (IsSqlite)
        {
            EmulateRowVersionOnSqlite(b);
        }
    }

    /// <summary>
    /// SQLite has no rowversion type. Tests run on SQLite, so emulate it with an application-managed
    /// concurrency token that changes on every tracked save.
    /// </summary>
    private static void EmulateRowVersionOnSqlite(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var prop = entity.FindProperty("RowVersion");
            if (prop is null) continue;
            prop.ValueGenerated = ValueGenerated.Never;
            prop.IsConcurrencyToken = true;
            prop.SetDefaultValueSql("randomblob(8)");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchSqliteRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TouchSqliteRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void TouchSqliteRowVersions()
    {
        if (!IsSqlite) return;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var prop = entry.Metadata.FindProperty("RowVersion");
            if (prop is null) continue;
            entry.Property("RowVersion").CurrentValue = RandomNumberGenerator.GetBytes(8);
        }
    }

    private sealed class ProviderAwareModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.GetType(), context.Database.ProviderName, designTime);
    }
}
