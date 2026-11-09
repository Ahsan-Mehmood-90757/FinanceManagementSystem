using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Administration;
using Fms.Domain.Documents;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fms.Infrastructure.Persistence;

// Enums are stored as readable strings (except the [Flags] PartyKind, which needs bitwise queries).

internal sealed class SystemSettingsConfig : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
    }
}

internal sealed class AppUserConfig : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class DocumentSequenceConfig : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(10);
        b.Property(x => x.Prefix).HasMaxLength(10);
    }
}

internal sealed class UnitConfig : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> b)
    {
        b.ToTable("Units");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Property(x => x.Symbol).HasMaxLength(15).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId);
        b.HasIndex(x => new { x.ParentId, x.Name });
    }
}

internal sealed class ExpenseCategoryConfig : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Sku).IsUnique();
        b.HasIndex(x => x.Name);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
    }
}

internal sealed class PartyConfig : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Kind).HasConversion<int>();
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.Balance);
    }
}

internal sealed class FinancialAccountConfig : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class InventoryBatchConfig : IEntityTypeConfiguration<InventoryBatch>
{
    public void Configure(EntityTypeBuilder<InventoryBatch> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Version).IsRowVersion();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        // FIFO lookups: product's open batches in receipt order.
        b.HasIndex(x => new { x.ProductId, x.ReceivedOn, x.CreatedAt });
        b.HasIndex(x => x.ExpiryDate);
    }
}

internal sealed class InventoryMovementConfig : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.DocumentNumber).HasMaxLength(30);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId);
        b.HasOne<InventoryBatch>().WithMany().HasForeignKey(x => x.BatchId);
        b.HasIndex(x => new { x.ProductId, x.Date });
        b.HasIndex(x => x.DocumentId);
    }
}

internal sealed class PartyLedgerConfig : IEntityTypeConfiguration<PartyLedgerEntry>
{
    public void Configure(EntityTypeBuilder<PartyLedgerEntry> b)
    {
        b.ToTable("PartyLedgerEntries");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.DocumentNumber).HasMaxLength(30);
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId);
        b.HasIndex(x => new { x.PartyId, x.Date });
        b.HasIndex(x => x.DocumentId);
    }
}

internal sealed class AccountLedgerConfig : IEntityTypeConfiguration<AccountLedgerEntry>
{
    public void Configure(EntityTypeBuilder<AccountLedgerEntry> b)
    {
        b.ToTable("AccountLedgerEntries");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.DocumentNumber).HasMaxLength(30);
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.AccountId);
        b.HasIndex(x => new { x.AccountId, x.Date });
        b.HasIndex(x => x.DocumentId);
    }
}

internal sealed class BillChargeConfig : IEntityTypeConfiguration<BillCharge>
{
    public void Configure(EntityTypeBuilder<BillCharge> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId);
        b.HasOne(x => x.PaidFromAccount).WithMany().HasForeignKey(x => x.PaidFromAccountId);
        b.ToTable(t => t.HasCheckConstraint("ck_bill_charges_one_parent",
            "(purchase_bill_id IS NULL) <> (sale_bill_id IS NULL)"));
    }
}

internal sealed class PurchaseBillConfig : IEntityTypeConfiguration<PurchaseBill>
{
    public void Configure(EntityTypeBuilder<PurchaseBill> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.Date);
        b.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseBillId);
        b.HasMany(x => x.Charges).WithOne().HasForeignKey(c => c.PurchaseBillId);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Charges).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseBillLineConfig : IEntityTypeConfiguration<PurchaseBillLine>
{
    public void Configure(EntityTypeBuilder<PurchaseBillLine> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId);
        b.HasIndex(x => x.BatchId).IsUnique();
    }
}

internal sealed class PurchaseReturnConfig : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.Date);
        b.HasOne(x => x.PurchaseBill).WithMany().HasForeignKey(x => x.PurchaseBillId);
        b.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseReturnId);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseReturnLineConfig : IEntityTypeConfiguration<PurchaseReturnLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnLine> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        b.HasOne<PurchaseBillLine>().WithMany().HasForeignKey(x => x.PurchaseBillLineId);
        b.HasOne<InventoryBatch>().WithMany().HasForeignKey(x => x.BatchId);
    }
}

internal sealed class SaleBillConfig : IEntityTypeConfiguration<SaleBill>
{
    public void Configure(EntityTypeBuilder<SaleBill> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.Date);
        b.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleBillId);
        b.HasMany(x => x.Charges).WithOne().HasForeignKey(c => c.SaleBillId);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Charges).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleBillLineConfig : IEntityTypeConfiguration<SaleBillLine>
{
    public void Configure(EntityTypeBuilder<SaleBillLine> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        b.HasMany(x => x.Allocations).WithOne().HasForeignKey(a => a.SaleBillLineId);
        b.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => x.ProductId);
    }
}

internal sealed class SaleLineAllocationConfig : IEntityTypeConfiguration<SaleLineAllocation>
{
    public void Configure(EntityTypeBuilder<SaleLineAllocation> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId);
    }
}

internal sealed class SaleReturnConfig : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.Date);
        b.HasOne(x => x.SaleBill).WithMany().HasForeignKey(x => x.SaleBillId);
        b.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleReturnId);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleReturnLineConfig : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        b.HasOne<SaleBillLine>().WithMany().HasForeignKey(x => x.SaleBillLineId);
    }
}

internal sealed class VoucherConfig : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LinkedDocumentType).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.Date);
        b.HasIndex(x => x.LinkedDocumentId);
        b.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
        b.HasOne(x => x.ToAccount).WithMany().HasForeignKey(x => x.ToAccountId);
        b.HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId);
    }
}

internal sealed class StockAdjustmentConfig : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId);
    }
}
