using Fms.Application.Common;
using Fms.Application.Parties;
using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Accounts;

public sealed record AccountDto(
    Guid Id, string Name, AccountType Type, string? BankName, string? AccountNumber,
    decimal OpeningBalance, decimal Balance, bool AllowNegative, bool IsActive);

public sealed record AccountRequest(
    string Name, AccountType Type, string? BankName, string? AccountNumber, bool AllowNegative, bool IsActive = true);

public sealed class AccountService(
    IRepository<FinancialAccount> accounts,
    IReadDb read,
    IUnitOfWork uow)
{
    public Task<List<AccountDto>> ListAsync(bool activeOnly, CancellationToken ct) =>
        read.Query<FinancialAccount>()
            .Where(a => !activeOnly || a.IsActive)
            .OrderBy(a => a.Type).ThenBy(a => a.Name)
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.BankName, a.AccountNumber, a.OpeningBalance, a.Balance, a.AllowNegative, a.IsActive))
            .ToListAsync(ct);

    public async Task<AccountDto> GetAsync(Guid id, CancellationToken ct) =>
        await read.Query<FinancialAccount>().Where(a => a.Id == id)
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.BankName, a.AccountNumber, a.OpeningBalance, a.Balance, a.AllowNegative, a.IsActive))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Account", id);

    public Task<Guid> CreateAsync(AccountRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        await EnsureUniqueName(r.Name, null, t);
        var account = FinancialAccount.Create(r.Name, r.Type, r.BankName, r.AccountNumber, r.AllowNegative);
        accounts.Add(account);
        return account.Id;
    }, ct);

    public Task<Guid> UpdateAsync(Guid id, AccountRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var account = await accounts.GetByIdAsync(id, t) ?? throw new NotFoundException("Account", id);
        await EnsureUniqueName(r.Name, id, t);
        if (!r.IsActive && account.Balance != 0)
            throw new ConflictException("An account with a non-zero balance cannot be deactivated. Transfer the balance first.");
        account.Update(r.Name, r.Type, r.BankName, r.AccountNumber, r.AllowNegative, r.IsActive);
        return id;
    }, ct);

    public async Task<StatementDto> StatementAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var account = await read.Query<FinancialAccount>().FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new NotFoundException("Account", id);
        var entries = read.Query<AccountLedgerEntry>().Where(e => e.AccountId == id);

        var opening = from is { } f
            ? await entries.Where(e => e.Date < f).SumAsync(e => e.AmountIn - e.AmountOut, ct)
            : 0m;

        var period = entries;
        if (from is { } f2) period = period.Where(e => e.Date >= f2);
        if (to is { } t2) period = period.Where(e => e.Date <= t2);
        var rows = await period.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync(ct);

        var running = opening;
        var lines = rows.Select(e =>
        {
            running += e.AmountIn - e.AmountOut;
            // For accounts: Debit column = money in, Credit column = money out.
            return new StatementLine(e.Date, e.DocumentType, e.DocumentId, e.DocumentNumber, e.Description,
                e.AmountIn, e.AmountOut, running, e.IsReversal);
        }).ToList();

        return new StatementDto(account.Id, account.Name, from, to, opening,
            lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), running, lines);
    }

    private async Task EnsureUniqueName(string name, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim().ToLower();
        if (await read.Query<FinancialAccount>().AnyAsync(a => a.Name.ToLower() == n && a.Id != exceptId, ct))
            throw new ConflictException($"An account named '{name.Trim()}' already exists.");
    }
}
