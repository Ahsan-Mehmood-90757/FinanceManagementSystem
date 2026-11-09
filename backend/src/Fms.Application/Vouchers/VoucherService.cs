using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.Documents;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Vouchers;

public sealed record VoucherRequest(
    VoucherType Type, DateOnly Date, Guid? PartyId, Guid AccountId, Guid? ToAccountId, Guid? ExpenseCategoryId,
    decimal Amount, string? Reference, string? Notes);

public sealed record VoucherDto(
    Guid Id, string Number, VoucherType Type, DateOnly Date,
    Guid? PartyId, string? PartyName, Guid AccountId, string AccountName, Guid? ToAccountId, string? ToAccountName,
    Guid? ExpenseCategoryId, string? ExpenseCategoryName, decimal Amount, string? Reference, string? Notes,
    DocumentStatus Status, DocumentType? LinkedDocumentType, Guid? LinkedDocumentId, string? LinkedDocumentNumber,
    DateTimeOffset CreatedAt, string? CreatedBy, DateTimeOffset? VoidedAt, string? VoidedBy, string? VoidReason);

public sealed record VoucherQuery : PageQuery
{
    public VoucherType? Type { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? PartyId { get; init; }
    public Guid? AccountId { get; init; }
    public bool IncludeVoided { get; init; } = true;
}

public sealed record VoidRequest(string Reason);

/// <summary>
/// Posts and reverses vouchers. Shared by <see cref="VoucherService"/> and by the bill services
/// when cash is settled at the time a bill is posted.
/// </summary>
public sealed class VoucherPoster(
    IRepository<Voucher> vouchers,
    LedgerWriter ledger,
    IDocumentNumberGenerator numbers,
    ICurrentUser user,
    IClock clock)
{
    public async Task<Voucher> ReceiptAsync(DateOnly date, Party party, FinancialAccount into, Money amount,
        string? reference, string? notes, CancellationToken ct)
    {
        var v = Voucher.Receipt(await numbers.NextAsync(SequenceKeys.Receipt, ct), date, party, into, amount, reference, notes);
        var doc = Ref(v);
        ledger.Deposit(into, date, doc, $"Receipt from {party.Name}", amount);
        ledger.CreditParty(party, date, doc, $"Received into {into.Name}", amount);
        vouchers.Add(v);
        return v;
    }

    public async Task<Voucher> PaymentAsync(DateOnly date, Party party, FinancialAccount from, Money amount,
        string? reference, string? notes, CancellationToken ct)
    {
        var v = Voucher.Payment(await numbers.NextAsync(SequenceKeys.Payment, ct), date, party, from, amount, reference, notes);
        var doc = Ref(v);
        ledger.Withdraw(from, date, doc, $"Payment to {party.Name}", amount);
        ledger.DebitParty(party, date, doc, $"Paid from {from.Name}", amount);
        vouchers.Add(v);
        return v;
    }

    public async Task<Voucher> ExpenseAsync(DateOnly date, ExpenseCategory category, FinancialAccount from, Money amount,
        string? reference, string? notes, CancellationToken ct)
    {
        var v = Voucher.Expense(await numbers.NextAsync(SequenceKeys.Expense, ct), date, category, from, amount, reference, notes);
        ledger.Withdraw(from, date, Ref(v), $"Expense: {category.Name}", amount);
        vouchers.Add(v);
        return v;
    }

    public async Task<Voucher> TransferAsync(DateOnly date, FinancialAccount from, FinancialAccount to, Money amount,
        string? reference, string? notes, CancellationToken ct)
    {
        var v = Voucher.Transfer(await numbers.NextAsync(SequenceKeys.Transfer, ct), date, from, to, amount, reference, notes);
        var doc = Ref(v);
        ledger.Withdraw(from, date, doc, $"Transfer to {to.Name}", amount);
        ledger.Deposit(to, date, doc, $"Transfer from {from.Name}", amount);
        vouchers.Add(v);
        return v;
    }

    /// <summary>Voids a voucher and posts equal-and-opposite ledger rows. Requires Party / Account / ToAccount loaded.</summary>
    public void Reverse(Voucher v, string reason)
    {
        v.Void(reason, user.UserName, clock.UtcNow);
        var doc = Ref(v);
        var amount = Money.Of(v.Amount);
        var note = $"VOID {v.Number}";
        switch (v.Type)
        {
            case VoucherType.Receipt:
                ledger.Withdraw(v.Account, v.Date, doc, note, amount, isReversal: true);
                ledger.DebitParty(v.Party!, v.Date, doc, note, amount, isReversal: true);
                break;
            case VoucherType.Payment:
                ledger.Deposit(v.Account, v.Date, doc, note, amount, isReversal: true);
                ledger.CreditParty(v.Party!, v.Date, doc, note, amount, isReversal: true);
                break;
            case VoucherType.Expense:
                ledger.Deposit(v.Account, v.Date, doc, note, amount, isReversal: true);
                break;
            case VoucherType.Transfer:
                ledger.Withdraw(v.ToAccount!, v.Date, doc, note, amount, isReversal: true);
                ledger.Deposit(v.Account, v.Date, doc, note, amount, isReversal: true);
                break;
            default:
                throw new DomainException("Unknown voucher type.");
        }
    }

    private static DocumentRef Ref(Voucher v) => new(DocumentType.Voucher, v.Id, v.Number);
}

public sealed class VoucherService(
    IRepository<Voucher> vouchers,
    IRepository<Party> parties,
    IRepository<FinancialAccount> accounts,
    IRepository<ExpenseCategory> expenseCategories,
    VoucherPoster poster,
    SystemState system,
    IReadDb read,
    IUnitOfWork uow)
{
    public static IQueryable<VoucherDto> Project(IQueryable<Voucher> q) =>
        q.Select(v => new VoucherDto(v.Id, v.Number, v.Type, v.Date,
            v.PartyId, v.Party != null ? v.Party.Name : null,
            v.AccountId, v.Account.Name,
            v.ToAccountId, v.ToAccount != null ? v.ToAccount.Name : null,
            v.ExpenseCategoryId, v.ExpenseCategory != null ? v.ExpenseCategory.Name : null,
            v.Amount, v.Reference, v.Notes, v.Status,
            v.LinkedDocumentType, v.LinkedDocumentId, v.LinkedDocumentNumber,
            v.CreatedAt, v.CreatedBy, v.VoidedAt, v.VoidedBy, v.VoidReason));

    public Task<PagedResult<VoucherDto>> ListAsync(VoucherQuery query, CancellationToken ct)
    {
        var q = read.Query<Voucher>();
        if (query.Type is { } type) q = q.Where(v => v.Type == type);
        if (query.From is { } from) q = q.Where(v => v.Date >= from);
        if (query.To is { } to) q = q.Where(v => v.Date <= to);
        if (query.PartyId is { } pid) q = q.Where(v => v.PartyId == pid);
        if (query.AccountId is { } aid) q = q.Where(v => v.AccountId == aid || v.ToAccountId == aid);
        if (!query.IncludeVoided) q = q.Where(v => v.Status == DocumentStatus.Posted);
        var s = query.NormalizedSearch();
        if (s is not null)
            q = q.Where(v => v.Number.ToLower().Contains(s)
                             || (v.Reference != null && v.Reference.ToLower().Contains(s))
                             || (v.Party != null && v.Party.Name.ToLower().Contains(s)));
        return Project(q.OrderByDescending(v => v.Date).ThenByDescending(v => v.CreatedAt)).ToPagedAsync(query, ct);
    }

    public async Task<VoucherDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(read.Query<Voucher>().Where(v => v.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Voucher", id);

    public Task<Guid> CreateAsync(VoucherRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        await system.EnsureOperationalAsync(r.Date, t);
        var amount = Money.Of(Guard.Positive(r.Amount, "Amount"));
        var account = await accounts.GetByIdAsync(r.AccountId, t) ?? throw new NotFoundException("Account", r.AccountId);

        Voucher voucher;
        switch (r.Type)
        {
            case VoucherType.Receipt or VoucherType.Payment:
            {
                if (r.PartyId is not { } pid) throw new ValidationException("A party is required for receipts and payments.");
                var party = await parties.GetByIdAsync(pid, t) ?? throw new NotFoundException("Party", pid);
                voucher = r.Type == VoucherType.Receipt
                    ? await poster.ReceiptAsync(r.Date, party, account, amount, r.Reference, r.Notes, t)
                    : await poster.PaymentAsync(r.Date, party, account, amount, r.Reference, r.Notes, t);
                break;
            }
            case VoucherType.Expense:
            {
                if (r.ExpenseCategoryId is not { } cid) throw new ValidationException("An expense category is required.");
                var category = await expenseCategories.GetByIdAsync(cid, t) ?? throw new NotFoundException("Expense category", cid);
                voucher = await poster.ExpenseAsync(r.Date, category, account, amount, r.Reference, r.Notes, t);
                break;
            }
            case VoucherType.Transfer:
            {
                if (r.ToAccountId is not { } toId) throw new ValidationException("A destination account is required.");
                var to = await accounts.GetByIdAsync(toId, t) ?? throw new NotFoundException("Account", toId);
                voucher = await poster.TransferAsync(r.Date, account, to, amount, r.Reference, r.Notes, t);
                break;
            }
            default:
                throw new ValidationException("Unknown voucher type.");
        }
        return voucher.Id;
    }, ct);

    public Task<Guid> VoidAsync(Guid id, VoidRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var voucher = await vouchers.Query()
                          .Include(v => v.Party).Include(v => v.Account).Include(v => v.ToAccount)
                          .FirstOrDefaultAsync(v => v.Id == id, t)
                      ?? throw new NotFoundException("Voucher", id);
        poster.Reverse(voucher, r.Reason);
        return id;
    }, ct);
}
