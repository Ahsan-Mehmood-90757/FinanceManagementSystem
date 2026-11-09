using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.Inventory;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Documents;

/// <summary>
/// Cash/bank movement document. Receipt and Payment settle party balances (unlinked - they are not
/// allocated to particular bills); Expense records standalone OpEx; Transfer moves money between accounts.
/// </summary>
public sealed class Voucher : PostedDocument
{
    private Voucher() { }

    public VoucherType Type { get; private set; }
    public Guid? PartyId { get; private set; }
    public Party? Party { get; private set; }
    /// <summary>Receipt: account receiving money. Payment / Expense / Transfer: account paying.</summary>
    public Guid AccountId { get; private set; }
    public FinancialAccount Account { get; private set; } = default!;
    public Guid? ToAccountId { get; private set; }
    public FinancialAccount? ToAccount { get; private set; }
    public Guid? ExpenseCategoryId { get; private set; }
    public ExpenseCategory? ExpenseCategory { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }

    /// <summary>Set when the voucher was created as part of posting another document (e.g. cash paid on a sale).</summary>
    public DocumentType? LinkedDocumentType { get; private set; }
    public Guid? LinkedDocumentId { get; private set; }
    public string? LinkedDocumentNumber { get; private set; }

    public static Voucher Receipt(string number, DateOnly date, Party party, FinancialAccount into, Money amount, string? reference, string? notes)
    {
        party.EnsureCanTransact(party.Kind);
        return Build(number, VoucherType.Receipt, date, into, amount, reference, notes, v =>
        {
            v.PartyId = party.Id;
            v.Party = party;
        });
    }

    public static Voucher Payment(string number, DateOnly date, Party party, FinancialAccount from, Money amount, string? reference, string? notes)
    {
        party.EnsureCanTransact(party.Kind);
        return Build(number, VoucherType.Payment, date, from, amount, reference, notes, v =>
        {
            v.PartyId = party.Id;
            v.Party = party;
        });
    }

    public static Voucher Expense(string number, DateOnly date, ExpenseCategory category, FinancialAccount from, Money amount, string? reference, string? notes)
    {
        if (!category.IsActive) throw new DomainException($"Expense category '{category.Name}' is inactive.");
        return Build(number, VoucherType.Expense, date, from, amount, reference, notes, v =>
        {
            v.ExpenseCategoryId = category.Id;
            v.ExpenseCategory = category;
        });
    }

    public static Voucher Transfer(string number, DateOnly date, FinancialAccount from, FinancialAccount to, Money amount, string? reference, string? notes)
    {
        if (from.Id == to.Id) throw new DomainException("Cannot transfer to the same account.");
        to.EnsureUsable();
        return Build(number, VoucherType.Transfer, date, from, amount, reference, notes, v =>
        {
            v.ToAccountId = to.Id;
            v.ToAccount = to;
        });
    }

    public void LinkTo(DocumentType type, Guid id, string number)
    {
        LinkedDocumentType = type;
        LinkedDocumentId = id;
        LinkedDocumentNumber = number;
    }

    public void Void(string reason, string? by, DateTimeOffset at) => MarkVoided(reason, by, at);

    private static Voucher Build(string number, VoucherType type, DateOnly date, FinancialAccount account, Money amount,
        string? reference, string? notes, Action<Voucher> configure)
    {
        if (!amount.IsPositive) throw new DomainException("Voucher amount must be greater than zero.");
        account.EnsureUsable();
        var v = new Voucher
        {
            Number = number,
            Type = type,
            Date = date,
            AccountId = account.Id,
            Account = account,
            Amount = amount.Amount,
            Reference = Guard.Optional(reference, "Reference", 100),
            Notes = Guard.Optional(notes, "Notes", 1000)
        };
        configure(v);
        return v;
    }
}

/// <summary>Write-off of stock from a batch (expired, damaged, lost). Valued at batch cost; charged to net profit.</summary>
public sealed class StockAdjustment : AuditableEntity
{
    private StockAdjustment() { }

    public string Number { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public Guid BatchId { get; private set; }
    public InventoryBatch Batch { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Value { get; private set; }
    public string Reason { get; private set; } = default!;

    public static StockAdjustment WriteOff(string number, DateOnly date, InventoryBatch batch, ValueObjects.Quantity quantity, string reason)
    {
        batch.Product.Unit.EnsureValid(quantity, batch.Product.Name);
        batch.Consume(quantity);
        return new StockAdjustment
        {
            Number = number,
            Date = date,
            ProductId = batch.ProductId,
            Product = batch.Product,
            BatchId = batch.Id,
            Batch = batch,
            Quantity = quantity.Value,
            UnitCost = batch.UnitCost,
            Value = Money.FromRate(quantity, batch.UnitCost).Amount,
            Reason = Guard.Required(reason, "Reason", 500)
        };
    }
}
