using Fms.Domain.Common;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Ledger;

/// <summary>Reference to the document that caused a ledger / stock movement.</summary>
public readonly record struct DocumentRef(DocumentType Type, Guid Id, string Number);

/// <summary>
/// Append-only party ledger row. Debit increases the party balance (they owe us more),
/// credit decreases it (we owe them more / they paid us).
/// </summary>
public sealed class PartyLedgerEntry : Entity
{
    private PartyLedgerEntry() { }

    public Guid PartyId { get; private set; }
    public DateOnly Date { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public Guid DocumentId { get; private set; }
    public string DocumentNumber { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public bool IsReversal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static PartyLedgerEntry Create(Guid partyId, DateOnly date, DocumentRef doc, string description,
        Money debit, Money credit, Money balanceAfter, bool isReversal, DateTimeOffset createdAt) => new()
    {
        PartyId = partyId,
        Date = date,
        DocumentType = doc.Type,
        DocumentId = doc.Id,
        DocumentNumber = doc.Number,
        Description = description,
        Debit = debit.Amount,
        Credit = credit.Amount,
        BalanceAfter = balanceAfter.Amount,
        IsReversal = isReversal,
        CreatedAt = createdAt
    };
}

/// <summary>Append-only cash/bank ledger row.</summary>
public sealed class AccountLedgerEntry : Entity
{
    private AccountLedgerEntry() { }

    public Guid AccountId { get; private set; }
    public DateOnly Date { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public Guid DocumentId { get; private set; }
    public string DocumentNumber { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal AmountIn { get; private set; }
    public decimal AmountOut { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public bool IsReversal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static AccountLedgerEntry Create(Guid accountId, DateOnly date, DocumentRef doc, string description,
        Money amountIn, Money amountOut, Money balanceAfter, bool isReversal, DateTimeOffset createdAt) => new()
    {
        AccountId = accountId,
        Date = date,
        DocumentType = doc.Type,
        DocumentId = doc.Id,
        DocumentNumber = doc.Number,
        Description = description,
        AmountIn = amountIn.Amount,
        AmountOut = amountOut.Amount,
        BalanceAfter = balanceAfter.Amount,
        IsReversal = isReversal,
        CreatedAt = createdAt
    };
}

/// <summary>Append-only stock card row for a batch.</summary>
public sealed class InventoryMovement : Entity
{
    private InventoryMovement() { }

    public Guid ProductId { get; private set; }
    public Guid BatchId { get; private set; }
    public DateOnly Date { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public Guid DocumentId { get; private set; }
    public string DocumentNumber { get; private set; } = default!;
    public decimal QuantityIn { get; private set; }
    public decimal QuantityOut { get; private set; }
    public decimal UnitCost { get; private set; }
    public bool IsReversal { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static InventoryMovement In(Guid productId, Guid batchId, DateOnly date, DocumentRef doc, Quantity qty,
        decimal unitCost, DateTimeOffset at, bool isReversal = false, string? note = null) => new()
    {
        ProductId = productId, BatchId = batchId, Date = date,
        DocumentType = doc.Type, DocumentId = doc.Id, DocumentNumber = doc.Number,
        QuantityIn = qty.Value, QuantityOut = 0m, UnitCost = unitCost,
        IsReversal = isReversal, Note = note, CreatedAt = at
    };

    public static InventoryMovement Out(Guid productId, Guid batchId, DateOnly date, DocumentRef doc, Quantity qty,
        decimal unitCost, DateTimeOffset at, bool isReversal = false, string? note = null) => new()
    {
        ProductId = productId, BatchId = batchId, Date = date,
        DocumentType = doc.Type, DocumentId = doc.Id, DocumentNumber = doc.Number,
        QuantityIn = 0m, QuantityOut = qty.Value, UnitCost = unitCost,
        IsReversal = isReversal, Note = note, CreatedAt = at
    };
}
