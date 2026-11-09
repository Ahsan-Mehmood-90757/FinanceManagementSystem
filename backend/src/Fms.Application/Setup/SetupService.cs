using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Administration;
using Fms.Domain.Common;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Setup;

public sealed record CompanyDto(string CompanyName, string? Address, string? Phone, string? Email, string? TaxNumber,
    string? InvoiceFooter, DateOnly GoLiveDate, bool IsOperational, DateTimeOffset? SetupCompletedAt, string? SetupCompletedBy);

public sealed record CompanyRequest(string CompanyName, string? Address, string? Phone, string? Email, string? TaxNumber,
    string? InvoiceFooter, DateOnly? GoLiveDate);

public sealed record SetupStatusDto(
    CompanyDto Company, DateOnly OpeningDate,
    int PartiesWithOpening, decimal OpeningReceivable, decimal OpeningPayable,
    int AccountsWithOpening, decimal OpeningCashBank,
    int OpeningBatches, decimal OpeningStockValue);

public sealed record OpeningBalanceRequest(decimal Amount);

public sealed record OpeningStockRequest(Guid ProductId, decimal Quantity, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate);

/// <summary>
/// Day-0 initialisation: company profile, go-live date, opening balances for parties, accounts and inventory.
/// Everything here is locked once <see cref="CompleteAsync"/> runs; from then on balances only move via documents.
/// </summary>
public sealed class SetupService(
    SystemState system,
    IRepository<Party> parties,
    IRepository<FinancialAccount> accounts,
    IRepository<Product> products,
    IRepository<InventoryBatch> batches,
    IRepository<InventoryMovement> movements,
    LedgerWriter ledger,
    ICurrentUser user,
    IClock clock,
    IReadDb read,
    IUnitOfWork uow)
{
    private static CompanyDto ToDto(SystemSettings s) => new(s.CompanyName, s.Address, s.Phone, s.Email, s.TaxNumber,
        s.InvoiceFooter, s.GoLiveDate, s.IsOperational, s.SetupCompletedAt, s.SetupCompletedBy);

    public async Task<CompanyDto> GetCompanyAsync(CancellationToken ct) => ToDto(await system.GetAsync(ct));

    public async Task<SetupStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var s = await system.GetAsync(ct);
        var partyOpenings = read.Query<Party>().Where(p => p.OpeningBalance != 0);
        var accountOpenings = read.Query<FinancialAccount>().Where(a => a.OpeningBalance != 0);
        var openingBatches = read.Query<InventoryBatch>().Where(b => b.Source == BatchSource.Opening);

        return new SetupStatusDto(ToDto(s), s.OpeningDate,
            await partyOpenings.CountAsync(ct),
            await partyOpenings.Where(p => p.OpeningBalance > 0).SumAsync(p => p.OpeningBalance, ct),
            await partyOpenings.Where(p => p.OpeningBalance < 0).SumAsync(p => -p.OpeningBalance, ct),
            await accountOpenings.CountAsync(ct),
            await accountOpenings.SumAsync(a => a.OpeningBalance, ct),
            await openingBatches.CountAsync(ct),
            await openingBatches.SumAsync(b => b.OriginalQuantity * b.UnitCost, ct));
    }

    public Task<CompanyDto> UpdateCompanyAsync(CompanyRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var s = await system.GetForUpdateAsync(t);
        s.UpdateCompany(r.CompanyName, r.Address, r.Phone, r.Email, r.TaxNumber, r.InvoiceFooter);
        if (r.GoLiveDate is { } goLive && goLive != s.GoLiveDate)
        {
            if (await HasOpeningEntries(t))
                throw new ConflictException("Remove the opening balances before changing the go-live date (they are dated the day before it).");
            s.SetGoLiveDate(goLive);
        }
        return ToDto(s);
    }, ct);

    public Task<Guid> SetPartyOpeningAsync(Guid partyId, OpeningBalanceRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var s = await system.GetForUpdateAsync(t);
        s.EnsureInSetup();
        var party = await parties.GetByIdAsync(partyId, t) ?? throw new NotFoundException("Party", partyId);
        ledger.AddPartyEntry(party.SetOpeningBalance(Money.Of(r.Amount), s.OpeningDate, clock.UtcNow));
        return partyId;
    }, ct);

    public Task<Guid> SetAccountOpeningAsync(Guid accountId, OpeningBalanceRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var s = await system.GetForUpdateAsync(t);
        s.EnsureInSetup();
        var account = await accounts.GetByIdAsync(accountId, t) ?? throw new NotFoundException("Account", accountId);
        ledger.AddAccountEntry(account.SetOpeningBalance(Money.Of(r.Amount), s.OpeningDate, clock.UtcNow));
        return accountId;
    }, ct);

    public Task<Guid> AddOpeningStockAsync(OpeningStockRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var s = await system.GetForUpdateAsync(t);
        s.EnsureInSetup();
        var product = await products.Query().Include(p => p.Unit).FirstOrDefaultAsync(p => p.Id == r.ProductId, t)
                      ?? throw new NotFoundException("Product", r.ProductId);
        var qty = Quantity.Positive(r.Quantity);
        var batch = InventoryBatch.Receive(product, r.BatchNumber, BatchSource.Opening, null, s.OpeningDate, r.ExpiryDate,
            qty, r.UnitCost, fallbackBatchNumber: $"OPEN-{product.Sku}-{clock.UtcNow:HHmmss}");
        batches.Add(batch);
        ledger.StockIn(batch, s.OpeningDate, new DocumentRef(DocumentType.Opening, batch.Id, "OPENING"), qty, note: "Opening stock");
        return batch.Id;
    }, ct);

    public Task<Guid> RemoveOpeningStockAsync(Guid batchId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var s = await system.GetForUpdateAsync(t);
        s.EnsureInSetup();
        var batch = await batches.GetByIdAsync(batchId, t) ?? throw new NotFoundException("Batch", batchId);
        if (batch.Source != BatchSource.Opening) throw new ConflictException("Only opening stock batches can be removed.");
        foreach (var m in await movements.Query().Where(m => m.BatchId == batchId).ToListAsync(t))
            movements.Remove(m);
        batches.Remove(batch);
        return batchId;
    }, ct);

    public Task<CompanyDto> CompleteAsync(CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var s = await system.GetForUpdateAsync(t);
        if (!await read.Query<FinancialAccount>().AnyAsync(a => a.IsActive, t))
            throw new DomainException("Create at least one cash or bank account before going live.", "no_accounts");
        s.CompleteSetup(user.UserName, clock.UtcNow);
        return ToDto(s);
    }, ct);

    private async Task<bool> HasOpeningEntries(CancellationToken ct) =>
        await read.Query<Party>().AnyAsync(p => p.OpeningBalance != 0, ct)
        || await read.Query<FinancialAccount>().AnyAsync(a => a.OpeningBalance != 0, ct)
        || await read.Query<InventoryBatch>().AnyAsync(b => b.Source == BatchSource.Opening, ct);
}
