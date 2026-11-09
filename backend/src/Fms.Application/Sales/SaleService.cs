using Fms.Application.Common;
using Fms.Application.Vouchers;
using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Common;
using Fms.Domain.Documents;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Fms.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Sales;

public sealed record BatchPickRequest(Guid BatchId, decimal Quantity);

/// <param name="UnitPrice">Null = use the product's default sale price.</param>
/// <param name="Batches">Optional manual batch override; when given it must cover the full quantity.</param>
public sealed record SaleLineRequest(Guid ProductId, decimal Quantity, decimal? UnitPrice, IReadOnlyList<BatchPickRequest>? Batches);

/// <param name="PartyId">Null = the built-in walk-in customer (must be paid in full).</param>
public sealed record SaleBillRequest(
    DateOnly Date, Guid? PartyId, string? CustomerReference, string? Notes,
    IReadOnlyList<SaleLineRequest> Lines,
    IReadOnlyList<ChargeRequest>? Charges,
    SettlementRequest? Payment);

public sealed record AllocationDto(Guid BatchId, string BatchNumber, DateOnly? ExpiryDate, decimal Quantity, decimal UnitCost,
    decimal Cost, decimal ReturnedQuantity);

public sealed record SaleLineDto(Guid Id, Guid ProductId, string ProductName, string Sku, string UnitSymbol,
    decimal Quantity, decimal UnitPrice, decimal LineTotal, decimal CostTotal, decimal ReturnedQuantity,
    IReadOnlyList<AllocationDto> Allocations);

public sealed record SaleBillDto(
    Guid Id, string Number, DateOnly Date, Guid PartyId, string PartyCode, string PartyName, string? PartyPhone,
    string? PartyAddress, string? CustomerReference, string? Notes, DocumentStatus Status,
    decimal Subtotal, decimal CostOfGoods, decimal GrossProfit, decimal ChargesTotal, decimal Total,
    IReadOnlyList<SaleLineDto> Lines, IReadOnlyList<ChargeDto> Charges,
    IReadOnlyList<LinkedVoucherDto> Settlements, IReadOnlyList<ReturnSummaryDto> Returns,
    DateTimeOffset CreatedAt, string? CreatedBy, DateTimeOffset? VoidedAt, string? VoidedBy, string? VoidReason);

public sealed record SaleReturnLineDto(Guid ProductId, string ProductName, string UnitSymbol, decimal Quantity,
    decimal UnitPrice, decimal Amount, decimal CostReversed);

public sealed record SaleReturnDto(Guid Id, string Number, DateOnly Date, Guid BillId, string BillNumber,
    Guid PartyId, string PartyName, string? Reason, decimal Total, decimal CostTotal,
    IReadOnlyList<SaleReturnLineDto> Lines, IReadOnlyList<LinkedVoucherDto> Refunds, DateTimeOffset CreatedAt, string? CreatedBy);

public sealed class SaleService(
    IRepository<SaleBill> bills,
    IRepository<SaleReturn> returns,
    IRepository<InventoryBatch> batches,
    IRepository<Party> parties,
    IRepository<Product> products,
    IRepository<ExpenseCategory> expenseCategories,
    IRepository<FinancialAccount> accounts,
    IRepository<Voucher> vouchers,
    VoucherPoster poster,
    LedgerWriter ledger,
    SystemState system,
    IDocumentNumberGenerator numbers,
    ICurrentUser user,
    IClock clock,
    IReadDb read,
    IUnitOfWork uow)
{
    // ------------------------------------------------------------------ queries
    public Task<PagedResult<DocumentListItem>> ListAsync(DateRangeQuery query, CancellationToken ct)
    {
        var q = read.Query<SaleBill>();
        if (query.From is { } from) q = q.Where(b => b.Date >= from);
        if (query.To is { } to) q = q.Where(b => b.Date <= to);
        if (query.PartyId is { } pid) q = q.Where(b => b.PartyId == pid);
        var s = query.NormalizedSearch();
        if (s is not null)
            q = q.Where(b => b.Number.ToLower().Contains(s) || b.Party.Name.ToLower().Contains(s)
                             || (b.CustomerReference != null && b.CustomerReference.ToLower().Contains(s)));
        return q.OrderByDescending(b => b.Date).ThenByDescending(b => b.CreatedAt)
            .Select(b => new DocumentListItem(b.Id, b.Number, b.Date, b.PartyId, b.Party.Name, b.Total, b.Status,
                b.Lines.Count(), b.CreatedBy, b.CustomerReference))
            .ToPagedAsync(query, ct);
    }

    public async Task<SaleBillDto> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await read.Query<SaleBill>()
                    .Include(x => x.Party)
                    .Include(x => x.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Unit)
                    .Include(x => x.Lines).ThenInclude(l => l.Allocations).ThenInclude(a => a.Batch)
                    .Include(x => x.Charges).ThenInclude(c => c.ExpenseCategory)
                    .Include(x => x.Charges).ThenInclude(c => c.PaidFromAccount)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Sale bill", id);

        var settlements = await LinkedVouchers(DocumentType.SaleBill, id, ct);
        var rets = await read.Query<SaleReturn>().Where(r => r.SaleBillId == id)
            .OrderBy(r => r.Date).Select(r => new ReturnSummaryDto(r.Id, r.Number, r.Date, r.Total)).ToListAsync(ct);

        return new SaleBillDto(b.Id, b.Number, b.Date, b.PartyId, b.Party.Code, b.Party.Name, b.Party.Phone, b.Party.Address,
            b.CustomerReference, b.Notes, b.Status, b.Subtotal, b.CostOfGoods, b.GrossProfit, b.ChargesTotal, b.Total,
            b.Lines.Select(l => new SaleLineDto(l.Id, l.ProductId, l.Product.Name, l.Product.Sku, l.Product.Unit.Symbol,
                l.Quantity, l.UnitPrice, l.LineTotal, l.CostTotal, l.ReturnedQuantity,
                l.Allocations.OrderBy(a => a.Sequence).Select(a => new AllocationDto(a.BatchId, a.Batch.BatchNumber, a.Batch.ExpiryDate,
                    a.Quantity, a.UnitCost, a.Cost, a.ReturnedQuantity)).ToList())).ToList(),
            b.Charges.Select(c => new ChargeDto(c.Id, c.ExpenseCategoryId, c.ExpenseCategory.Name, c.Description, c.Amount,
                c.PaidFromAccountId, c.PaidFromAccount?.Name)).ToList(),
            settlements, rets, b.CreatedAt, b.CreatedBy, b.VoidedAt, b.VoidedBy, b.VoidReason);
    }

    private Task<List<LinkedVoucherDto>> LinkedVouchers(DocumentType type, Guid id, CancellationToken ct) =>
        read.Query<Voucher>().Where(v => v.LinkedDocumentType == type && v.LinkedDocumentId == id)
            .OrderBy(v => v.CreatedAt)
            .Select(v => new LinkedVoucherDto(v.Id, v.Number, v.Type, v.Date, v.Account.Name, v.Amount, v.Status))
            .ToListAsync(ct);

    // ------------------------------------------------------------------ post
    public Task<Guid> PostAsync(SaleBillRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        if (r.Lines is not { Count: > 0 }) throw new ValidationException("Add at least one line.");
        await system.EnsureOperationalAsync(r.Date, t);

        var party = r.PartyId is { } pid
            ? await parties.GetByIdAsync(pid, t) ?? throw new NotFoundException("Customer", pid)
            : await parties.Query().FirstOrDefaultAsync(p => p.IsSystem, t)
              ?? throw new DomainException("Walk-in customer is missing; restart the API to seed it.");

        var number = await numbers.NextAsync(SequenceKeys.SaleBill, t);
        var bill = SaleBill.Create(number, r.Date, party, r.CustomerReference, r.Notes);
        var doc = new DocumentRef(DocumentType.SaleBill, bill.Id, number);

        var productIds = r.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productMap = await products.Query().Include(p => p.Unit)
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, t);
        var missing = productIds.FirstOrDefault(id => !productMap.ContainsKey(id));
        if (missing != Guid.Empty) throw new NotFoundException("Product", missing);

        // Tracked batches, shared across lines so two lines of the same product see each other's consumption.
        var saleDate = r.Date;
        var batchesByProduct = (await batches.Query()
                .Where(b => productIds.Contains(b.ProductId) && b.RemainingQuantity > 0 && b.ReceivedOn <= saleDate)
                .ToListAsync(t))
            .GroupBy(b => b.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var i = 0; i < r.Lines.Count; i++)
        {
            var lr = r.Lines[i];
            var product = productMap[lr.ProductId];
            var qty = Quantity.Positive(lr.Quantity, $"Line {i + 1} quantity");
            var candidates = batchesByProduct.TryGetValue(product.Id, out var list) ? list : new List<InventoryBatch>();
            var picks = lr.Batches?.Where(p => p.Quantity > 0).Select(p => new ManualBatchPick(p.BatchId, Quantity.Of(p.Quantity))).ToList();

            var allocations = FifoAllocator.Allocate(candidates, qty, r.Date, product.Name, picks);
            bill.AddLine(product, qty, lr.UnitPrice ?? product.DefaultSalePrice, allocations);
            foreach (var a in allocations)
                ledger.StockOut(a.Batch, r.Date, doc, a.Quantity);
        }

        foreach (var c in r.Charges ?? Array.Empty<ChargeRequest>())
        {
            if (c.PaidFromAccountId is not { } aid)
                throw new ValidationException("Charges on a sale must be paid from a cash/bank account.");
            var category = await expenseCategories.GetByIdAsync(c.ExpenseCategoryId, t)
                           ?? throw new NotFoundException("Expense category", c.ExpenseCategoryId);
            var from = await accounts.GetByIdAsync(aid, t) ?? throw new NotFoundException("Account", aid);
            var amount = Money.Of(c.Amount);
            bill.AddCharge(category, c.Description, amount, from);
            ledger.Withdraw(from, r.Date, doc, $"{category.Name} on {number}", amount);
        }

        bill.EnsureHasLines();
        bills.Add(bill);
        ledger.DebitParty(party, r.Date, doc,
            string.IsNullOrWhiteSpace(bill.CustomerReference) ? "Sale" : $"Sale (ref {bill.CustomerReference})", Money.Of(bill.Total));

        var paid = r.Payment?.Amount ?? 0m;
        if (party.IsSystem && Money.Of(paid) != Money.Of(bill.Total))
            throw new DomainException(
                $"Walk-in sales must be paid exactly in full ({Money.Of(bill.Total)}). Select a customer to sell on credit or take an advance.", "walkin_unpaid");

        if (r.Payment is { Amount: > 0 } pay)
        {
            var account = await accounts.GetByIdAsync(pay.AccountId, t) ?? throw new NotFoundException("Account", pay.AccountId);
            var voucher = await poster.ReceiptAsync(r.Date, party, account, Money.Of(pay.Amount), pay.Reference, $"Received on {number}", t);
            voucher.LinkTo(DocumentType.SaleBill, bill.Id, number);
        }
        return bill.Id;
    }, ct);

    // ------------------------------------------------------------------ void
    public Task<Guid> VoidAsync(Guid id, VoidRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var bill = await bills.Query()
                       .Include(b => b.Party)
                       .Include(b => b.Lines).ThenInclude(l => l.Allocations).ThenInclude(a => a.Batch)
                       .Include(b => b.Charges).ThenInclude(c => c.PaidFromAccount)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(b => b.Id == id, t)
                   ?? throw new NotFoundException("Sale bill", id);

        bill.Void(r.Reason, user.UserName, clock.UtcNow);
        var doc = new DocumentRef(DocumentType.SaleBill, bill.Id, bill.Number);
        var note = $"VOID {bill.Number}";

        foreach (var allocation in bill.Lines.SelectMany(l => l.Allocations))
        {
            var qty = Quantity.Of(allocation.Quantity);
            allocation.Batch.Restore(qty);
            ledger.StockIn(allocation.Batch, bill.Date, doc, qty, isReversal: true, note: note);
        }

        foreach (var charge in bill.Charges.Where(c => c.PaidFromAccount is not null))
            ledger.Deposit(charge.PaidFromAccount!, bill.Date, doc, note, Money.Of(charge.Amount), isReversal: true);

        ledger.CreditParty(bill.Party, bill.Date, doc, note, Money.Of(bill.Total), isReversal: true);

        var linked = await vouchers.Query().Include(v => v.Party).Include(v => v.Account).Include(v => v.ToAccount)
            .Where(v => v.LinkedDocumentType == DocumentType.SaleBill && v.LinkedDocumentId == id && v.Status == DocumentStatus.Posted)
            .ToListAsync(t);
        foreach (var v in linked)
            poster.Reverse(v, $"{bill.Number} voided: {r.Reason}");

        return id;
    }, ct);

    // ------------------------------------------------------------------ returns
    public Task<PagedResult<ReturnListItem>> ListReturnsAsync(DateRangeQuery query, CancellationToken ct)
    {
        var q = read.Query<SaleReturn>();
        if (query.From is { } from) q = q.Where(x => x.Date >= from);
        if (query.To is { } to) q = q.Where(x => x.Date <= to);
        if (query.PartyId is { } pid) q = q.Where(x => x.PartyId == pid);
        var s = query.NormalizedSearch();
        if (s is not null) q = q.Where(x => x.Number.ToLower().Contains(s) || x.SaleBill.Number.ToLower().Contains(s) || x.Party.Name.ToLower().Contains(s));
        return q.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt)
            .Select(x => new ReturnListItem(x.Id, x.Number, x.Date, x.SaleBillId, x.SaleBill.Number, x.PartyId, x.Party.Name,
                x.Total, x.Reason, x.CreatedBy))
            .ToPagedAsync(query, ct);
    }

    public async Task<SaleReturnDto> GetReturnAsync(Guid id, CancellationToken ct)
    {
        var x = await read.Query<SaleReturn>()
                    .Include(r => r.Party).Include(r => r.SaleBill)
                    .Include(r => r.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Unit)
                    .FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("Sale return", id);
        var refunds = await LinkedVouchers(DocumentType.SaleReturn, id, ct);
        return new SaleReturnDto(x.Id, x.Number, x.Date, x.SaleBillId, x.SaleBill.Number, x.PartyId, x.Party.Name, x.Reason,
            x.Total, x.CostTotal,
            x.Lines.Select(l => new SaleReturnLineDto(l.ProductId, l.Product.Name, l.Product.Unit.Symbol, l.Quantity, l.UnitPrice,
                l.Amount, l.CostReversed)).ToList(),
            refunds, x.CreatedAt, x.CreatedBy);
    }

    public Task<Guid> ReturnAsync(Guid billId, ReturnRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        if (r.Lines is not { Count: > 0 }) throw new ValidationException("Select at least one line to return.");
        await system.EnsureOperationalAsync(r.Date, t);

        var bill = await bills.Query()
                       .Include(b => b.Party)
                       .Include(b => b.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Unit)
                       .Include(b => b.Lines).ThenInclude(l => l.Allocations).ThenInclude(a => a.Batch)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(b => b.Id == billId, t)
                   ?? throw new NotFoundException("Sale bill", billId);

        var number = await numbers.NextAsync(SequenceKeys.SaleReturn, t);
        var ret = SaleReturn.Create(number, r.Date, bill, r.Reason);
        var doc = new DocumentRef(DocumentType.SaleReturn, ret.Id, number);

        foreach (var lr in r.Lines.Where(l => l.Quantity > 0))
        {
            var line = bill.Lines.FirstOrDefault(l => l.Id == lr.BillLineId)
                       ?? throw new ValidationException($"Line {lr.BillLineId} is not on {bill.Number}.");
            var restorations = ret.AddLine(line, Quantity.Positive(lr.Quantity));
            foreach (var (allocation, qty) in restorations)
                ledger.StockIn(allocation.Batch, r.Date, doc, qty, note: $"Customer return ({bill.Number})");
        }
        ret.EnsureHasLines();
        returns.Add(ret);
        ledger.CreditParty(bill.Party, r.Date, doc, $"Sale return against {bill.Number}", Money.Of(ret.Total));

        if (r.Refund is { Amount: > 0 } refund)
        {
            var account = await accounts.GetByIdAsync(refund.AccountId, t) ?? throw new NotFoundException("Account", refund.AccountId);
            var voucher = await poster.PaymentAsync(r.Date, bill.Party, account, Money.Of(refund.Amount), refund.Reference,
                $"Refund for {number}", t);
            voucher.LinkTo(DocumentType.SaleReturn, ret.Id, number);
        }
        return ret.Id;
    }, ct);
}
