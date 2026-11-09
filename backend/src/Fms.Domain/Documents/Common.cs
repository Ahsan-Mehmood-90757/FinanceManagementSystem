using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.MasterData;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Documents;

/// <summary>Base for immutable posted documents. Once posted the only state change allowed is a void (reversal).</summary>
public abstract class PostedDocument : AuditableEntity
{
    public string Number { get; protected set; } = default!;
    public DateOnly Date { get; protected set; }
    public string? Notes { get; protected set; }
    public DocumentStatus Status { get; protected set; } = DocumentStatus.Posted;
    public DateTimeOffset? VoidedAt { get; protected set; }
    public string? VoidedBy { get; protected set; }
    public string? VoidReason { get; protected set; }

    public bool IsVoided => Status == DocumentStatus.Voided;

    protected void MarkVoided(string reason, string? by, DateTimeOffset at)
    {
        if (IsVoided) throw new DomainException($"{Number} is already voided.", "already_voided");
        VoidReason = Guard.Required(reason, "Void reason", 500);
        VoidedBy = by;
        VoidedAt = at;
        Status = DocumentStatus.Voided;
    }
}

/// <summary>
/// Operational cost attached to a purchase or sale bill (freight, labour, equipment...).
/// Traced to the bill but excluded from COGS / gross margin; it reduces net profit.
/// Paid either from a financial account, or (purchase bills only) added to the supplier's bill.
/// </summary>
public sealed class BillCharge : Entity
{
    private BillCharge() { }

    public Guid? PurchaseBillId { get; private set; }
    public Guid? SaleBillId { get; private set; }
    public Guid ExpenseCategoryId { get; private set; }
    public ExpenseCategory ExpenseCategory { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? PaidFromAccountId { get; private set; }
    public FinancialAccount? PaidFromAccount { get; private set; }

    public bool IsOnPartyBill => PaidFromAccountId is null;

    internal static BillCharge ForPurchase(Guid billId, ExpenseCategory category, string? description, Money amount, FinancialAccount? paidFrom)
        => Create(category, description, amount, paidFrom, purchaseBillId: billId, saleBillId: null);

    internal static BillCharge ForSale(Guid billId, ExpenseCategory category, string? description, Money amount, FinancialAccount paidFrom)
        => Create(category, description, amount, paidFrom, purchaseBillId: null, saleBillId: billId);

    private static BillCharge Create(ExpenseCategory category, string? description, Money amount, FinancialAccount? paidFrom,
        Guid? purchaseBillId, Guid? saleBillId)
    {
        if (!amount.IsPositive) throw new DomainException("Charge amount must be greater than zero.");
        if (!category.IsActive) throw new DomainException($"Expense category '{category.Name}' is inactive.");
        paidFrom?.EnsureUsable();
        return new BillCharge
        {
            PurchaseBillId = purchaseBillId,
            SaleBillId = saleBillId,
            ExpenseCategoryId = category.Id,
            ExpenseCategory = category,
            Description = Guard.Optional(description, "Charge description", 200),
            Amount = amount.Amount,
            PaidFromAccountId = paidFrom?.Id,
            PaidFromAccount = paidFrom
        };
    }
}
