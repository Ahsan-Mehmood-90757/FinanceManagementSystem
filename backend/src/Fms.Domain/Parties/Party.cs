using Fms.Domain.Common;
using Fms.Domain.Ledger;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Parties;

/// <summary>
/// Unified customer / supplier master. <see cref="Balance"/> is a single unlinked running balance:
/// positive = receivable (they owe us), negative = payable or advance held (we owe them).
/// </summary>
public sealed class Party : AuditableEntity, IConcurrencyGuarded
{
    private Party() { }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public PartyKind Kind { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? TaxNumber { get; private set; }
    public string? Notes { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Marks the built-in walk-in / cash customer.</summary>
    public bool IsSystem { get; private set; }
    public uint Version { get; private set; }

    public static Party Create(string code, string name, PartyKind kind, string? phone, string? email, string? address,
        string? taxNumber, string? notes, bool isSystem = false)
    {
        var p = new Party { IsSystem = isSystem };
        p.Update(code, name, kind, phone, email, address, taxNumber, notes, true);
        return p;
    }

    public void Update(string code, string name, PartyKind kind, string? phone, string? email, string? address,
        string? taxNumber, string? notes, bool isActive)
    {
        if (!Enum.IsDefined(kind)) throw new DomainException("Invalid party kind.");
        if (IsSystem && !isActive) throw new DomainException("The walk-in customer cannot be deactivated.");
        Code = Guard.Required(code, "Party code", 30).ToUpperInvariant();
        Name = Guard.Required(name, "Party name", 200);
        Kind = kind;
        Phone = Guard.Optional(phone, "Phone", 30);
        Email = Guard.Optional(email, "Email", 200);
        Address = Guard.Optional(address, "Address", 500);
        TaxNumber = Guard.Optional(taxNumber, "Tax number", 50);
        Notes = Guard.Optional(notes, "Notes", 1000);
        IsActive = isActive;
    }

    public bool IsCustomer => Kind.HasFlag(PartyKind.Customer);
    public bool IsSupplier => Kind.HasFlag(PartyKind.Supplier);

    public void EnsureActive()
    {
        if (!IsActive) throw new DomainException($"Party '{Name}' is inactive.", "inactive_party");
    }

    public void EnsureCanTransact(PartyKind role)
    {
        EnsureActive();
        if (!Kind.HasFlag(role))
            throw new DomainException($"Party '{Name}' is not set up as a {role.ToString().ToLowerInvariant()}.", "party_role");
    }

    /// <summary>Day-0 opening balance. Posts the difference so the ledger always reconciles.</summary>
    public PartyLedgerEntry? SetOpeningBalance(Money amount, DateOnly date, DateTimeOffset at)
    {
        var delta = amount - Money.Of(OpeningBalance);
        if (delta.IsZero) return null;
        OpeningBalance = amount.Amount;
        var doc = new DocumentRef(DocumentType.Opening, Id, "OPENING");
        return delta.IsPositive
            ? Post(date, doc, "Opening balance", delta, Money.Zero, false, at)
            : Post(date, doc, "Opening balance", Money.Zero, delta.Abs(), false, at);
    }

    /// <summary>Debit: party owes us more (sale, payment made to them, purchase return).</summary>
    public PartyLedgerEntry Debit(DateOnly date, DocumentRef doc, string description, Money amount, DateTimeOffset at, bool isReversal = false)
        => Post(date, doc, description, amount, Money.Zero, isReversal, at);

    /// <summary>Credit: party owes us less / we owe them more (purchase, receipt from them, sale return).</summary>
    public PartyLedgerEntry Credit(DateOnly date, DocumentRef doc, string description, Money amount, DateTimeOffset at, bool isReversal = false)
        => Post(date, doc, description, Money.Zero, amount, isReversal, at);

    private PartyLedgerEntry Post(DateOnly date, DocumentRef doc, string description, Money debit, Money credit, bool isReversal, DateTimeOffset at)
    {
        if (debit.IsNegative || credit.IsNegative)
            throw new DomainException("Ledger amounts cannot be negative.");
        var next = Money.Of(Balance) + debit - credit;
        Balance = next.Amount;
        return PartyLedgerEntry.Create(Id, date, doc, description, debit, credit, next, isReversal, at);
    }
}
