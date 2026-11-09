using System.Text;
using Fms.Application.Common;
using Fms.Domain.Accounts;
using Fms.Domain.Administration;
using Fms.Domain.Common;
using Fms.Domain.Documents;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace Fms.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, IClock clock)
    : DbContext(options), IReadDb
{
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();

    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<PartyLedgerEntry> PartyLedger => Set<PartyLedgerEntry>();
    public DbSet<AccountLedgerEntry> AccountLedger => Set<AccountLedgerEntry>();

    public DbSet<PurchaseBill> PurchaseBills => Set<PurchaseBill>();
    public DbSet<PurchaseBillLine> PurchaseBillLines => Set<PurchaseBillLine>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();
    public DbSet<SaleBill> SaleBills => Set<SaleBill>();
    public DbSet<SaleBillLine> SaleBillLines => Set<SaleBillLine>();
    public DbSet<SaleLineAllocation> SaleLineAllocations => Set<SaleLineAllocation>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();
    public DbSet<BillCharge> BillCharges => Set<BillCharge>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    /// <summary>CQRS-lite read side: never tracked.</summary>
    IQueryable<T> IReadDb.Query<T>() => Set<T>().AsNoTracking();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Money is rounded to 2 dp in the domain; 4 dp storage keeps unit costs / fractional quantities exact.
        builder.Properties<decimal>().HavePrecision(18, 4);
        builder.Properties<string>().HaveMaxLength(1000);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // We generate UUID v7 keys in the domain; tell EF never to treat a set key as "existing row".
            var id = entity.FindProperty("Id");
            if (id is not null && id.ClrType == typeof(Guid))
                id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;

            // Financial data is never cascade-deleted.
            foreach (var fk in entity.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        ApplySnakeCaseNames(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var who = currentUser.UserName ?? "system";
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy ??= who;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = who;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>snake_case tables/columns so the schema is pleasant to browse in DBeaver / psql.</summary>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is not null) entity.SetTableName(ToSnake(table));

            foreach (var property in entity.GetProperties())
            {
                // The concurrency token maps to PostgreSQL's system column xmin; leave it alone.
                if (property.IsConcurrencyToken && property.ClrType == typeof(uint)) continue;
                property.SetColumnName(ToSnake(property.Name));
            }
            foreach (var key in entity.GetKeys())
                key.SetName(ToSnake(key.GetName()!));
            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnake(fk.GetConstraintName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnake(index.GetDatabaseName()!));
        }
    }

    private static string ToSnake(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_' && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
