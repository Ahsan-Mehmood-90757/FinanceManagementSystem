using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Ledger;
using Fms.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Parties;

public sealed record PartyDto(
    Guid Id, string Code, string Name, PartyKind Kind, string? Phone, string? Email, string? Address,
    string? TaxNumber, string? Notes, decimal OpeningBalance, decimal Balance, bool IsActive, bool IsSystem);

public sealed record PartyRequest(
    string? Code, string Name, PartyKind Kind, string? Phone, string? Email, string? Address,
    string? TaxNumber, string? Notes, bool IsActive = true);

public enum BalanceFilter { Any = 0, Receivable = 1, Payable = 2, Settled = 3 }

public sealed record PartyQuery : PageQuery
{
    public PartyKind? Kind { get; init; }
    public BalanceFilter Balance { get; init; }
    public bool ActiveOnly { get; init; }
}

public sealed record StatementLine(
    DateOnly Date, DocumentType DocumentType, Guid DocumentId, string DocumentNumber, string Description,
    decimal Debit, decimal Credit, decimal RunningBalance, bool IsReversal);

public sealed record StatementDto(
    Guid Id, string Name, DateOnly? From, DateOnly? To, decimal OpeningBalance,
    decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance, IReadOnlyList<StatementLine> Lines);

public sealed class PartyService(
    IRepository<Party> parties,
    IDocumentNumberGenerator numbers,
    IReadDb read,
    IUnitOfWork uow)
{
    private static IQueryable<PartyDto> Project(IQueryable<Party> q) =>
        q.Select(p => new PartyDto(p.Id, p.Code, p.Name, p.Kind, p.Phone, p.Email, p.Address, p.TaxNumber, p.Notes,
            p.OpeningBalance, p.Balance, p.IsActive, p.IsSystem));

    public Task<PagedResult<PartyDto>> ListAsync(PartyQuery query, CancellationToken ct)
    {
        var q = read.Query<Party>();
        var s = query.NormalizedSearch();
        if (s is not null)
            q = q.Where(p => p.Name.ToLower().Contains(s) || p.Code.ToLower().Contains(s) || (p.Phone != null && p.Phone.Contains(s)));
        if (query.Kind is { } kind) q = q.Where(p => (p.Kind & kind) == kind);
        if (query.ActiveOnly) q = q.Where(p => p.IsActive);
        q = query.Balance switch
        {
            BalanceFilter.Receivable => q.Where(p => p.Balance > 0),
            BalanceFilter.Payable => q.Where(p => p.Balance < 0),
            BalanceFilter.Settled => q.Where(p => p.Balance == 0),
            _ => q
        };
        return Project(q.OrderBy(p => p.Name)).ToPagedAsync(query, ct);
    }

    public async Task<PartyDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(read.Query<Party>().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Party", id);

    public Task<Guid> CreateAsync(PartyRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var code = string.IsNullOrWhiteSpace(r.Code) ? await numbers.NextAsync(SequenceKeys.Party, t) : r.Code;
        await EnsureUniqueCode(code, null, t);
        var party = Party.Create(code, r.Name, r.Kind, r.Phone, r.Email, r.Address, r.TaxNumber, r.Notes);
        parties.Add(party);
        return party.Id;
    }, ct);

    public Task<Guid> UpdateAsync(Guid id, PartyRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var party = await parties.GetByIdAsync(id, t) ?? throw new NotFoundException("Party", id);
        var code = string.IsNullOrWhiteSpace(r.Code) ? party.Code : r.Code;
        await EnsureUniqueCode(code, id, t);
        party.Update(code, r.Name, r.Kind, r.Phone, r.Email, r.Address, r.TaxNumber, r.Notes, r.IsActive);
        return id;
    }, ct);

    public async Task<StatementDto> StatementAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var party = await read.Query<Party>().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Party", id);
        var entries = read.Query<PartyLedgerEntry>().Where(e => e.PartyId == id);

        var opening = from is { } f
            ? await entries.Where(e => e.Date < f).SumAsync(e => e.Debit - e.Credit, ct)
            : 0m;

        var period = entries;
        if (from is { } f2) period = period.Where(e => e.Date >= f2);
        if (to is { } t2) period = period.Where(e => e.Date <= t2);
        var rows = await period.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync(ct);

        var running = opening;
        var lines = rows.Select(e =>
        {
            running += e.Debit - e.Credit;
            return new StatementLine(e.Date, e.DocumentType, e.DocumentId, e.DocumentNumber, e.Description,
                e.Debit, e.Credit, running, e.IsReversal);
        }).ToList();

        return new StatementDto(party.Id, party.Name, from, to, opening,
            lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), running, lines);
    }

    private async Task EnsureUniqueCode(string code, Guid? exceptId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await read.Query<Party>().AnyAsync(p => p.Code == normalized && p.Id != exceptId, ct))
            throw new ConflictException($"Party code '{normalized}' is already in use.");
    }
}
