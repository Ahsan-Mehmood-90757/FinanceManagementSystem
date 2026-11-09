using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.Inventory;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;
using Qty = Fms.Domain.ValueObjects.Quantity;

namespace Fms.Domain.Documents;

public sealed class SaleBill : PostedDocument
{
    private readonly List<SaleBillLine> _lines = [];
    private readonly List<BillCharge> _charges = [];

    private SaleBill() { }

    public Guid PartyId { get; private set; }
    public Party Party { get; private set; } = default!;
    public string? CustomerReference { get; private set; }
    /// <summary>Revenue: sum of line totals.</summary>
    public decimal Subtotal { get; private set; }
    /// <summary>FIFO cost of the goods sold.</summary>
    public decimal CostOfGoods { get; private set; }
    /// <summary>Operational charges we paid to deliver this sale (not part of COGS).</summary>
    public decimal ChargesTotal { get; private set; }
    /// <summary>Amount posted to the customer's ledger.</summary>
    public decimal Total { get; private set; }
    public decimal GrossProfit => Subtotal - CostOfGoods;

    public IReadOnlyCollection<SaleBillLine> Lines => _lines;
    public IReadOnlyCollection<BillCharge> Charges => _charges;

    public static SaleBill Create(string number, DateOnly date, Party customer, string? customerReference, string? notes)
    {
        customer.EnsureCanTransact(PartyKind.Customer);
        return new SaleBill
        {
            Number = number,
            Date = date,
            PartyId = customer.Id,
            Party = customer,
            CustomerReference = Guard.Optional(customerReference, "Customer reference", 100),
            Notes = Guard.Optional(notes, "Notes", 1000)
        };
    }

    /// <summary>Adds a line and consumes stock from the allocated batches.</summary>
    public SaleBillLine AddLine(Product product, Qty quantity, decimal unitPrice, IReadOnlyList<BatchAllocation> allocations)
    {
        product.EnsureSellable();
        product.Unit.EnsureValid(quantity, product.Name);
        if (Qty.Sum(allocations.Select(a => a.Quantity)) != quantity)
            throw new DomainException($"Batch allocations for '{product.Name}' do not match the line quantity.");

        var line = new SaleBillLine(Id, product, quantity, unitPrice);
        var sequence = 0;
        foreach (var allocation in allocations)
        {
            if (allocation.Batch.ProductId != product.Id)
                throw new DomainException("Allocated batch belongs to a different product.");
            allocation.Batch.Consume(allocation.Quantity);
            line.AddAllocation(allocation, sequence++);
        }
        _lines.Add(line);
        Recalculate();
        return line;
    }

    public BillCharge AddCharge(ExpenseCategory category, string? description, Money amount, FinancialAccount paidFrom)
    {
        var charge = BillCharge.ForSale(Id, category, description, amount, paidFrom);
        _charges.Add(charge);
        Recalculate();
        return charge;
    }

    public void EnsureHasLines()
    {
        if (_lines.Count == 0) throw new DomainException("A sale bill needs at least one line.", "no_lines");
    }

    public bool HasReturns => _lines.Any(l => l.ReturnedQuantity > 0m);

    public void Void(string reason, string? by, DateTimeOffset at)
    {
        if (HasReturns)
            throw new DomainException($"{Number} has sale returns and cannot be voided.", "has_returns");
        MarkVoided(reason, by, at);
    }

    private void Recalculate()
    {
        Subtotal = Money.Sum(_lines.Select(l => Money.Of(l.LineTotal))).Amount;
        CostOfGoods = Money.Sum(_lines.Select(l => Money.Of(l.CostTotal))).Amount;
        ChargesTotal = Money.Sum(_charges.Select(c => Money.Of(c.Amount))).Amount;
        Total = Subtotal;
    }
}

public sealed class SaleBillLine : Entity
{
    private readonly List<SaleLineAllocation> _allocations = [];

    private SaleBillLine() { }

    internal SaleBillLine(Guid billId, Product product, Qty quantity, decimal unitPrice)
    {
        SaleBillId = billId;
        ProductId = product.Id;
        Product = product;
        Quantity = quantity.Value;
        UnitPrice = decimal.Round(Guard.NonNegative(unitPrice, "Unit price"), 4);
        LineTotal = Money.FromRate(quantity, UnitPrice).Amount;
    }

    public Guid SaleBillId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal CostTotal { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    public IReadOnlyCollection<SaleLineAllocation> Allocations => _allocations;

    public Qty ReturnableQuantity => Qty.Of(Quantity - ReturnedQuantity);

    internal void AddAllocation(BatchAllocation allocation, int sequence)
    {
        _allocations.Add(new SaleLineAllocation(Id, allocation.Batch, allocation.Quantity, sequence));
        CostTotal = Money.Sum(_allocations.Select(a => Money.Of(a.Cost))).Amount;
    }

    /// <summary>
    /// Registers a return and decides which batches the goods go back to: the most recently allocated
    /// batches first (reverse of FIFO), so each unit returns at the cost it left at.
    /// </summary>
    internal IReadOnlyList<(SaleLineAllocation Allocation, Qty Quantity)> RegisterReturn(Qty quantity)
    {
        if (!quantity.IsPositive) throw new DomainException("Return quantity must be greater than zero.");
        if (quantity > ReturnableQuantity)
            throw new DomainException(
                $"Cannot return {quantity} of '{Product.Name}'; only {ReturnableQuantity} is returnable on this bill.", "return_exceeds");

        var result = new List<(SaleLineAllocation, Qty)>();
        var outstanding = quantity;
        foreach (var allocation in _allocations.OrderByDescending(a => a.Sequence))
        {
            if (outstanding.IsZero) break;
            var take = Qty.Min(outstanding, allocation.ReturnableQuantity);
            if (!take.IsPositive) continue;
            allocation.RegisterReturn(take);
            result.Add((allocation, take));
            outstanding -= take;
        }
        ReturnedQuantity = (Qty.Of(ReturnedQuantity) + quantity).Value;
        return result;
    }
}

/// <summary>Which batch (and at what cost) a sale line drew its stock from.</summary>
public sealed class SaleLineAllocation : Entity
{
    private SaleLineAllocation() { }

    internal SaleLineAllocation(Guid lineId, InventoryBatch batch, Qty quantity, int sequence)
    {
        SaleBillLineId = lineId;
        BatchId = batch.Id;
        Batch = batch;
        Quantity = quantity.Value;
        UnitCost = batch.UnitCost;
        Cost = Money.FromRate(quantity, batch.UnitCost).Amount;
        Sequence = sequence;
    }

    public Guid SaleBillLineId { get; private set; }
    public Guid BatchId { get; private set; }
    public InventoryBatch Batch { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Cost { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    public int Sequence { get; private set; }

    public Qty ReturnableQuantity => Qty.Of(Quantity - ReturnedQuantity);

    internal void RegisterReturn(Qty quantity) => ReturnedQuantity = (Qty.Of(ReturnedQuantity) + quantity).Value;
}

/// <summary>Goods a customer brings back against a specific sale bill. Stock goes back into the original batches.</summary>
public sealed class SaleReturn : AuditableEntity
{
    private readonly List<SaleReturnLine> _lines = [];

    private SaleReturn() { }

    public string Number { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public Guid SaleBillId { get; private set; }
    public SaleBill SaleBill { get; private set; } = default!;
    public Guid PartyId { get; private set; }
    public Party Party { get; private set; } = default!;
    public string? Reason { get; private set; }
    /// <summary>Revenue reversed (credited to the customer).</summary>
    public decimal Total { get; private set; }
    /// <summary>COGS reversed.</summary>
    public decimal CostTotal { get; private set; }
    public IReadOnlyCollection<SaleReturnLine> Lines => _lines;

    public static SaleReturn Create(string number, DateOnly date, SaleBill bill, string? reason)
    {
        if (bill.IsVoided) throw new DomainException($"{bill.Number} is voided.", "voided");
        if (date < bill.Date) throw new DomainException("Return date cannot be before the bill date.");
        return new SaleReturn
        {
            Number = number,
            Date = date,
            SaleBillId = bill.Id,
            SaleBill = bill,
            PartyId = bill.PartyId,
            Party = bill.Party,
            Reason = Guard.Optional(reason, "Reason", 500)
        };
    }

    /// <summary>Registers the return and restores stock. Returns the per-batch restorations for the stock card.</summary>
    public IReadOnlyList<(SaleLineAllocation Allocation, Qty Quantity)> AddLine(SaleBillLine billLine, Qty quantity)
    {
        if (billLine.SaleBillId != SaleBillId) throw new DomainException("Line belongs to a different bill.");
        billLine.Product.Unit.EnsureValid(quantity, billLine.Product.Name);

        var restorations = billLine.RegisterReturn(quantity);
        foreach (var (allocation, qty) in restorations)
            allocation.Batch.Restore(qty);

        var cost = Money.Sum(restorations.Select(r => Money.FromRate(r.Quantity, r.Allocation.UnitCost)));
        _lines.Add(new SaleReturnLine(Id, billLine, quantity, cost));
        Total = Money.Sum(_lines.Select(l => Money.Of(l.Amount))).Amount;
        CostTotal = Money.Sum(_lines.Select(l => Money.Of(l.CostReversed))).Amount;
        return restorations;
    }

    public void EnsureHasLines()
    {
        if (_lines.Count == 0) throw new DomainException("A return needs at least one line.", "no_lines");
    }
}

public sealed class SaleReturnLine : Entity
{
    private SaleReturnLine() { }

    internal SaleReturnLine(Guid returnId, SaleBillLine billLine, Qty quantity, Money costReversed)
    {
        SaleReturnId = returnId;
        SaleBillLineId = billLine.Id;
        ProductId = billLine.ProductId;
        Quantity = quantity.Value;
        UnitPrice = billLine.UnitPrice;
        Amount = Money.FromRate(quantity, billLine.UnitPrice).Amount;
        CostReversed = costReversed.Amount;
    }

    public Guid SaleReturnId { get; private set; }
    public Guid SaleBillLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount { get; private set; }
    public decimal CostReversed { get; private set; }
}
