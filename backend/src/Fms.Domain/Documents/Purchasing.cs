using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.Inventory;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Documents;

public sealed class PurchaseBill : PostedDocument
{
    private readonly List<PurchaseBillLine> _lines = [];
    private readonly List<BillCharge> _charges = [];

    private PurchaseBill() { }

    public Guid PartyId { get; private set; }
    public Party Party { get; private set; } = default!;
    public string? SupplierReference { get; private set; }
    /// <summary>Sum of line totals (goods value).</summary>
    public decimal Subtotal { get; private set; }
    /// <summary>Charges the supplier added to their bill (increase what we owe them).</summary>
    public decimal ChargesOnParty { get; private set; }
    /// <summary>All attached operational charges, whoever was paid.</summary>
    public decimal ChargesTotal { get; private set; }
    /// <summary>Amount posted to the supplier's ledger: Subtotal + ChargesOnParty.</summary>
    public decimal Total { get; private set; }

    public IReadOnlyCollection<PurchaseBillLine> Lines => _lines;
    public IReadOnlyCollection<BillCharge> Charges => _charges;

    public static PurchaseBill Create(string number, DateOnly date, Party supplier, string? supplierReference, string? notes)
    {
        supplier.EnsureCanTransact(PartyKind.Supplier);
        return new PurchaseBill
        {
            Number = number,
            Date = date,
            PartyId = supplier.Id,
            Party = supplier,
            SupplierReference = Guard.Optional(supplierReference, "Supplier reference", 100),
            Notes = Guard.Optional(notes, "Notes", 1000)
        };
    }

    public PurchaseBillLine AddLine(Product product, InventoryBatch batch)
    {
        if (batch.ProductId != product.Id) throw new DomainException("Batch does not belong to the product.");
        if (!product.IsActive) throw new DomainException($"Product '{product.Name}' is inactive.", "inactive_product");
        var line = new PurchaseBillLine(Id, product, batch);
        _lines.Add(line);
        Recalculate();
        return line;
    }

    public BillCharge AddCharge(ExpenseCategory category, string? description, Money amount, FinancialAccount? paidFrom)
    {
        var charge = BillCharge.ForPurchase(Id, category, description, amount, paidFrom);
        _charges.Add(charge);
        Recalculate();
        return charge;
    }

    public void EnsureHasLines()
    {
        if (_lines.Count == 0) throw new DomainException("A purchase bill needs at least one line.", "no_lines");
    }

    public bool HasReturns => _lines.Any(l => l.ReturnedQuantity > 0m);

    public void Void(string reason, string? by, DateTimeOffset at)
    {
        if (HasReturns)
            throw new DomainException($"{Number} has purchase returns and cannot be voided.", "has_returns");
        MarkVoided(reason, by, at);
    }

    private void Recalculate()
    {
        var subtotal = Money.Sum(_lines.Select(l => Money.Of(l.LineTotal)));
        var onParty = Money.Sum(_charges.Where(c => c.IsOnPartyBill).Select(c => Money.Of(c.Amount)));
        Subtotal = subtotal.Amount;
        ChargesOnParty = onParty.Amount;
        ChargesTotal = Money.Sum(_charges.Select(c => Money.Of(c.Amount))).Amount;
        Total = (subtotal + onParty).Amount;
    }
}

public sealed class PurchaseBillLine : Entity
{
    private PurchaseBillLine() { }

    internal PurchaseBillLine(Guid billId, Product product, InventoryBatch batch)
    {
        PurchaseBillId = billId;
        ProductId = product.Id;
        Product = product;
        BatchId = batch.Id;
        Batch = batch;
        Quantity = batch.OriginalQuantity;
        UnitCost = batch.UnitCost;
        LineTotal = Money.FromRate(batch.Original, UnitCost).Amount;
    }

    public Guid PurchaseBillId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public Guid BatchId { get; private set; }
    public InventoryBatch Batch { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal ReturnedQuantity { get; private set; }

    public Quantity ReturnableQuantity => ValueObjects.Quantity.Of(Quantity - ReturnedQuantity);

    internal void RegisterReturn(Quantity quantity)
    {
        if (!quantity.IsPositive) throw new DomainException("Return quantity must be greater than zero.");
        if (quantity > ReturnableQuantity)
            throw new DomainException(
                $"Cannot return {quantity} of '{Product.Name}'; only {ReturnableQuantity} is returnable on this bill.", "return_exceeds");
        ReturnedQuantity = (ValueObjects.Quantity.Of(ReturnedQuantity) + quantity).Value;
    }
}

/// <summary>Goods sent back to a supplier against a specific purchase bill. Reduces what we owe them.</summary>
public sealed class PurchaseReturn : AuditableEntity
{
    private readonly List<PurchaseReturnLine> _lines = [];

    private PurchaseReturn() { }

    public string Number { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public Guid PurchaseBillId { get; private set; }
    public PurchaseBill PurchaseBill { get; private set; } = default!;
    public Guid PartyId { get; private set; }
    public Party Party { get; private set; } = default!;
    public string? Reason { get; private set; }
    public decimal Total { get; private set; }
    public IReadOnlyCollection<PurchaseReturnLine> Lines => _lines;

    public static PurchaseReturn Create(string number, DateOnly date, PurchaseBill bill, string? reason)
    {
        if (bill.IsVoided) throw new DomainException($"{bill.Number} is voided.", "voided");
        if (date < bill.Date) throw new DomainException("Return date cannot be before the bill date.");
        return new PurchaseReturn
        {
            Number = number,
            Date = date,
            PurchaseBillId = bill.Id,
            PurchaseBill = bill,
            PartyId = bill.PartyId,
            Party = bill.Party,
            Reason = Guard.Optional(reason, "Reason", 500)
        };
    }

    /// <summary>Registers the return on the bill line and takes the stock back out of the line's batch.</summary>
    public PurchaseReturnLine AddLine(PurchaseBillLine billLine, Quantity quantity)
    {
        if (billLine.PurchaseBillId != PurchaseBillId) throw new DomainException("Line belongs to a different bill.");
        billLine.Product.Unit.EnsureValid(quantity, billLine.Product.Name);
        if (quantity > billLine.Batch.Remaining)
            throw new DomainException(
                $"Only {billLine.Batch.Remaining} of '{billLine.Product.Name}' from batch {billLine.Batch.BatchNumber} is still in stock; the rest has been sold or written off.",
                "return_exceeds_stock");
        billLine.RegisterReturn(quantity);
        billLine.Batch.Consume(quantity);

        var line = new PurchaseReturnLine(Id, billLine, quantity);
        _lines.Add(line);
        Total = Money.Sum(_lines.Select(l => Money.Of(l.Amount))).Amount;
        return line;
    }

    public void EnsureHasLines()
    {
        if (_lines.Count == 0) throw new DomainException("A return needs at least one line.", "no_lines");
    }
}

public sealed class PurchaseReturnLine : Entity
{
    private PurchaseReturnLine() { }

    internal PurchaseReturnLine(Guid returnId, PurchaseBillLine billLine, Quantity quantity)
    {
        PurchaseReturnId = returnId;
        PurchaseBillLineId = billLine.Id;
        ProductId = billLine.ProductId;
        BatchId = billLine.BatchId;
        Quantity = quantity.Value;
        UnitCost = billLine.UnitCost;
        Amount = Money.FromRate(quantity, billLine.UnitCost).Amount;
    }

    public Guid PurchaseReturnId { get; private set; }
    public Guid PurchaseBillLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public Guid BatchId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Amount { get; private set; }
}
