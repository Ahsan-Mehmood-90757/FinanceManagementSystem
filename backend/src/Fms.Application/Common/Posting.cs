using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Administration;
using Fms.Domain.Common;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Common;

/// <summary>
/// Writes balance changes and their append-only ledger rows together, so a balance can never move
/// without an audit row explaining why.
/// </summary>
public sealed class LedgerWriter(
    IRepository<PartyLedgerEntry> partyLedger,
    IRepository<AccountLedgerEntry> accountLedger,
    IRepository<InventoryMovement> movements,
    IClock clock)
{
    public void DebitParty(Party party, DateOnly date, DocumentRef doc, string description, Money amount, bool isReversal = false)
    {
        if (amount.IsZero) return;
        partyLedger.Add(party.Debit(date, doc, description, amount, clock.UtcNow, isReversal));
    }

    public void CreditParty(Party party, DateOnly date, DocumentRef doc, string description, Money amount, bool isReversal = false)
    {
        if (amount.IsZero) return;
        partyLedger.Add(party.Credit(date, doc, description, amount, clock.UtcNow, isReversal));
    }

    public void Deposit(FinancialAccount account, DateOnly date, DocumentRef doc, string description, Money amount, bool isReversal = false)
    {
        if (amount.IsZero) return;
        accountLedger.Add(account.Deposit(date, doc, description, amount, clock.UtcNow, isReversal));
    }

    public void Withdraw(FinancialAccount account, DateOnly date, DocumentRef doc, string description, Money amount, bool isReversal = false)
    {
        if (amount.IsZero) return;
        accountLedger.Add(account.Withdraw(date, doc, description, amount, clock.UtcNow, isReversal));
    }

    public void AddPartyEntry(PartyLedgerEntry? entry)
    {
        if (entry is not null) partyLedger.Add(entry);
    }

    public void AddAccountEntry(AccountLedgerEntry? entry)
    {
        if (entry is not null) accountLedger.Add(entry);
    }

    public void StockIn(InventoryBatch batch, DateOnly date, DocumentRef doc, Quantity qty, bool isReversal = false, string? note = null)
        => movements.Add(InventoryMovement.In(batch.ProductId, batch.Id, date, doc, qty, batch.UnitCost, clock.UtcNow, isReversal, note));

    public void StockOut(InventoryBatch batch, DateOnly date, DocumentRef doc, Quantity qty, bool isReversal = false, string? note = null)
        => movements.Add(InventoryMovement.Out(batch.ProductId, batch.Id, date, doc, qty, batch.UnitCost, clock.UtcNow, isReversal, note));
}

/// <summary>Access to the singleton settings row and Day-0 rules.</summary>
public sealed class SystemState(IRepository<SystemSettings> settings, IReadDb read)
{
    public async Task<SystemSettings> GetForUpdateAsync(CancellationToken ct) =>
        await settings.GetByIdAsync(SystemSettings.SingletonId, ct)
        ?? throw new DomainException("System settings are missing; restart the API to seed them.");

    public async Task<SystemSettings> GetAsync(CancellationToken ct) =>
        await read.Query<SystemSettings>().FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, ct)
        ?? throw new DomainException("System settings are missing; restart the API to seed them.");

    /// <summary>Throws unless setup is completed and <paramref name="date"/> is on/after go-live.</summary>
    public async Task EnsureOperationalAsync(DateOnly date, CancellationToken ct) =>
        (await GetAsync(ct)).EnsureOperational(date);
}

public static class DocRefs
{
    public static DocumentRef For(DocumentType type, Guid id, string number) => new(type, id, number);
}
