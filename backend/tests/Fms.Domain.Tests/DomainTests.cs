using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.Documents;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Tests;

public class FifoTests
{
    private static readonly DateOnly Day1 = new(2026, 1, 1);
    private static Product Oil(bool tracksExpiry = false) =>
        Product.Create("OIL", "Cooking Oil", null, null, UnitOfMeasure.Create("Litre", "L", true), 500m, 0m, tracksExpiry);

    private static InventoryBatch Batch(Product p, int dayOffset, decimal qty, decimal cost, DateOnly? expiry = null) =>
        InventoryBatch.Receive(p, null, BatchSource.Purchase, null, Day1.AddDays(dayOffset), expiry, Quantity.Of(qty), cost, $"B{dayOffset}");

    [Fact]
    public void Allocates_oldest_batches_first()
    {
        var p = Oil();
        var newer = Batch(p, 5, 10, 120);
        var older = Batch(p, 0, 4, 100);

        var result = FifoAllocator.Allocate([newer, older], Quantity.Of(6), Day1.AddDays(10), p.Name);

        Assert.Equal(2, result.Count);
        Assert.Same(older, result[0].Batch);
        Assert.Equal(4m, result[0].Quantity.Value);
        Assert.Same(newer, result[1].Batch);
        Assert.Equal(2m, result[1].Quantity.Value);
        Assert.Equal(400m + 240m, result.Sum(a => a.Cost.Amount));
    }

    [Fact]
    public void Supports_fractional_quantities()
    {
        var p = Oil();
        var b = Batch(p, 0, 1, 300);
        var result = FifoAllocator.Allocate([b], Quantity.Of(0.3m), Day1, p.Name);
        Assert.Equal(0.3m, result.Single().Quantity.Value);
        Assert.Equal(90m, result.Single().Cost.Amount);
    }

    [Fact]
    public void Throws_when_stock_is_insufficient()
    {
        var p = Oil();
        var ex = Assert.Throws<InsufficientStockException>(() =>
            FifoAllocator.Allocate([Batch(p, 0, 2, 100)], Quantity.Of(3), Day1, p.Name));
        Assert.Equal(2m, ex.Available);
    }

    [Fact]
    public void Skips_expired_batches()
    {
        var p = Oil(tracksExpiry: true);
        var expired = Batch(p, 0, 5, 100, expiry: Day1.AddDays(3));
        var fresh = Batch(p, 1, 5, 110, expiry: Day1.AddDays(60));

        var result = FifoAllocator.Allocate([expired, fresh], Quantity.Of(2), Day1.AddDays(10), p.Name);

        Assert.Same(fresh, result.Single().Batch);
    }

    [Fact]
    public void Manual_picks_override_fifo_and_must_match_quantity()
    {
        var p = Oil();
        var older = Batch(p, 0, 5, 100);
        var newer = Batch(p, 1, 5, 110);

        var picked = FifoAllocator.Allocate([older, newer], Quantity.Of(3), Day1.AddDays(2), p.Name,
            [new ManualBatchPick(newer.Id, Quantity.Of(3))]);
        Assert.Same(newer, picked.Single().Batch);

        Assert.Throws<DomainException>(() => FifoAllocator.Allocate([older, newer], Quantity.Of(3), Day1.AddDays(2), p.Name,
            [new ManualBatchPick(newer.Id, Quantity.Of(2))]));
    }

    [Fact]
    public void Whole_unit_products_reject_fractions()
    {
        var p = Product.Create("PEN", "Pen", null, null, UnitOfMeasure.Create("Pieces", "pcs", false), 10m, 0m, false);
        Assert.Throws<DomainException>(() => Batch(p, 0, 1.5m, 5));
    }
}

public class SaleAndReturnTests
{
    private static readonly DateOnly Day1 = new(2026, 1, 1);

    [Fact]
    public void Sale_consumes_batches_and_return_restores_latest_allocation_first()
    {
        var unit = UnitOfMeasure.Create("Pieces", "pcs", false);
        var product = Product.Create("A", "Widget", null, null, unit, 200m, 0m, false);
        var b1 = InventoryBatch.Receive(product, null, BatchSource.Purchase, null, Day1, null, Quantity.Of(3), 100m, "B1");
        var b2 = InventoryBatch.Receive(product, null, BatchSource.Purchase, null, Day1.AddDays(1), null, Quantity.Of(10), 150m, "B2");
        var customer = Party.Create("C1", "Ali Traders", PartyKind.Customer, null, null, null, null, null);

        var bill = SaleBill.Create("SB-000001", Day1.AddDays(2), customer, null, null);
        var allocations = FifoAllocator.Allocate([b1, b2], Quantity.Of(5), Day1.AddDays(2), product.Name);
        var line = bill.AddLine(product, Quantity.Of(5), 200m, allocations);

        Assert.Equal(0m, b1.RemainingQuantity);
        Assert.Equal(8m, b2.RemainingQuantity);
        Assert.Equal(1000m, bill.Subtotal);
        Assert.Equal(300m + 300m, bill.CostOfGoods);
        Assert.Equal(400m, bill.GrossProfit);

        var ret = SaleReturn.Create("SR-000001", Day1.AddDays(3), bill, "damaged");
        ret.AddLine(line, Quantity.Of(3));

        // 2 units go back to B2 (allocated last), then 1 to B1.
        Assert.Equal(10m, b2.RemainingQuantity);
        Assert.Equal(1m, b1.RemainingQuantity);
        Assert.Equal(600m, ret.Total);
        Assert.Equal(300m + 100m, ret.CostTotal);
        Assert.Throws<DomainException>(() => bill.Void("mistake", "test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Purchase_return_cannot_exceed_unsold_stock()
    {
        var unit = UnitOfMeasure.Create("Pieces", "pcs", false);
        var product = Product.Create("A", "Widget", null, null, unit, 200m, 0m, false);
        var supplier = Party.Create("S1", "Supplier", PartyKind.Supplier, null, null, null, null, null);
        var bill = PurchaseBill.Create("PB-000001", Day1, supplier, null, null);
        var batch = InventoryBatch.Receive(product, null, BatchSource.Purchase, bill.Id, Day1, null, Quantity.Of(10), 50m, "B");
        var line = bill.AddLine(product, batch);
        batch.Consume(Quantity.Of(8)); // sold elsewhere

        var ret = PurchaseReturn.Create("PR-000001", Day1, bill, null);
        Assert.Throws<DomainException>(() => ret.AddLine(line, Quantity.Of(3)));
        ret.AddLine(line, Quantity.Of(2));
        Assert.Equal(0m, batch.RemainingQuantity);
        Assert.Equal(100m, ret.Total);
    }
}

public class LedgerTests
{
    private static readonly DateOnly Day = new(2026, 1, 1);
    private static DocumentRef Doc => new(DocumentType.SaleBill, Guid.NewGuid(), "SB-1");

    [Fact]
    public void Party_balance_positive_means_receivable()
    {
        var p = Party.Create("C1", "Customer", PartyKind.Both, null, null, null, null, null);
        p.Debit(Day, Doc, "sale", Money.Of(1000), DateTimeOffset.UtcNow);
        var entry = p.Credit(Day, Doc, "receipt", Money.Of(1500), DateTimeOffset.UtcNow);

        Assert.Equal(-500m, p.Balance);          // we now hold an advance
        Assert.Equal(-500m, entry.BalanceAfter);
    }

    [Fact]
    public void Cash_account_cannot_go_negative()
    {
        var cash = FinancialAccount.Create("Cash", AccountType.Cash, null, null, false);
        cash.Deposit(Day, Doc, "in", Money.Of(100), DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => cash.Withdraw(Day, Doc, "out", Money.Of(100.01m), DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => FinancialAccount.Create("Cash 2", AccountType.Cash, null, null, allowNegative: true));
    }

    [Fact]
    public void Money_rounds_to_paisa()
    {
        Assert.Equal(10.13m, Money.Of(10.125m).Amount);
        Assert.Equal(33.33m, Money.FromRate(Quantity.Of(1), 33.3333m).Amount);
        Assert.Equal(99.99m, Money.FromRate(Quantity.Of(3), 33.33m).Amount);
    }
}
