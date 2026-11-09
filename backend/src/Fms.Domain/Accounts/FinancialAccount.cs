using Fms.Domain.Common;
using Fms.Domain.Ledger;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Accounts;

/// <summary>A real cash drawer, bank account or mobile wallet with a running balance.</summary>
public sealed class FinancialAccount : AuditableEntity, IConcurrencyGuarded
{
    private FinancialAccount() { }

    public string Name { get; private set; } = default!;
    public AccountType Type { get; private set; }
    public string? BankName { get; private set; }
    public string? AccountNumber { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal Balance { get; private set; }
    /// <summary>Allows overdraft (e.g. a running-finance bank facility). Cash can never go negative.</summary>
    public bool AllowNegative { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint Version { get; private set; }

    public static FinancialAccount Create(string name, AccountType type, string? bankName, string? accountNumber, bool allowNegative)
    {
        var a = new FinancialAccount();
        a.Update(name, type, bankName, accountNumber, allowNegative, true);
        return a;
    }

    public void Update(string name, AccountType type, string? bankName, string? accountNumber, bool allowNegative, bool isActive)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Invalid account type.");
        if (type == AccountType.Cash && allowNegative)
            throw new DomainException("Cash accounts cannot allow a negative balance.");
        if (!allowNegative && Balance < 0)
            throw new DomainException("Account is overdrawn; cannot disable negative balance until it is cleared.");
        Name = Guard.Required(name, "Account name", 100);
        Type = type;
        BankName = Guard.Optional(bankName, "Bank name", 100);
        AccountNumber = Guard.Optional(accountNumber, "Account number", 50);
        AllowNegative = allowNegative;
        IsActive = isActive;
    }

    public void EnsureUsable()
    {
        if (!IsActive) throw new DomainException($"Account '{Name}' is inactive.", "inactive_account");
    }

    public AccountLedgerEntry? SetOpeningBalance(Money amount, DateOnly date, DateTimeOffset at)
    {
        var delta = amount - Money.Of(OpeningBalance);
        if (delta.IsZero) return null;
        if (amount.IsNegative && !AllowNegative)
            throw new DomainException($"Account '{Name}' cannot have a negative opening balance.");
        OpeningBalance = amount.Amount;
        var doc = new DocumentRef(DocumentType.Opening, Id, "OPENING");
        return delta.IsPositive
            ? Post(date, doc, "Opening balance", delta, Money.Zero, false, at, enforceLimit: false)
            : Post(date, doc, "Opening balance", Money.Zero, delta.Abs(), false, at, enforceLimit: false);
    }

    public AccountLedgerEntry Deposit(DateOnly date, DocumentRef doc, string description, Money amount, DateTimeOffset at, bool isReversal = false)
        => Post(date, doc, description, amount, Money.Zero, isReversal, at, enforceLimit: true);

    public AccountLedgerEntry Withdraw(DateOnly date, DocumentRef doc, string description, Money amount, DateTimeOffset at, bool isReversal = false)
        => Post(date, doc, description, Money.Zero, amount, isReversal, at, enforceLimit: true);

    private AccountLedgerEntry Post(DateOnly date, DocumentRef doc, string description, Money amountIn, Money amountOut,
        bool isReversal, DateTimeOffset at, bool enforceLimit)
    {
        if (amountIn.IsNegative || amountOut.IsNegative)
            throw new DomainException("Ledger amounts cannot be negative.");
        var current = Money.Of(Balance);
        var next = current + amountIn - amountOut;
        if (enforceLimit && next.IsNegative && !AllowNegative)
            throw new DomainException(
                $"Insufficient funds in '{Name}': balance {current}, required {amountOut}.", "insufficient_funds");
        Balance = next.Amount;
        return AccountLedgerEntry.Create(Id, date, doc, description, amountIn, amountOut, next, isReversal, at);
    }
}
