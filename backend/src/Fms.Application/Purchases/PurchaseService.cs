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

namespace Fms.Application.Purchases;

public sealed record PurchaseLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost, string? BatchNumber, DateOnly? ExpiryDate);

public sealed record PurchaseBillRequest(
    DateOnly Date, Guid PartyId, string? SupplierReference, string? Notes,
    IReadOnlyList<PurchaseLineRequest> Lines,
    IReadOnlyList<ChargeRequest>? Charges,
    SettlementRequest? Payment);

public sealed record PurchaseLineDto(Guid Id, Guid ProductId, string ProductName, string Sku, string UnitSymbol,
    decimal Quantity, decimal UnitCost, decimal LineTotal, decimal ReturnedQuantity,
    Guid BatchId, string BatchNumber, DateOnly? ExpiryDate, decimal BatchRemaining);

public sealed record PurchaseBillDto(
    Guid Id, string Number, DateOnly Date, Guid PartyId, string PartyCode, string PartyName, string? SupplierReference,
    string? Notes, DocumentStatus Status, decimal Subtotal, decimal ChargesOnParty, decimal ChargesTotal, decimal Total,
    IReadOnlyList<PurchaseLineDto> Lines, IReadOnlyList<ChargeDto> Charges,
    IReadOnlyList<LinkedVoucherDto> Settlements, IReadOnlyList<ReturnSummaryDto> Returns,
    DateTimeOffset CreatedAt, string? CreatedBy, DateTimeOffset? VoidedAt, string? VoidedBy, string? VoidReason);

public sealed record PurchaseReturnLineDto(Guid ProductId, string ProductName, string UnitSymbol, decimal Quantity, decimal UnitCost, decimal Amount);

public sealed record PurchaseReturnDto(Guid Id, string Number, DateOnly Date, Guid BillId, string BillNumber,
    Guid PartyId, string PartyName, string? Reason, decimal Total, IReadOnlyList<PurchaseReturnLineDto> Lines,
    IReadOnlyList<LinkedVoucherDto> Refunds, DateTimeOffset CreatedAt, string? CreatedBy);

public sealed class PurchaseService(
    IRepository<PurchaseBill> bills,
    IRepository<PurchaseReturn> returns,
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
        var q = read.Query<PurchaseBill>();
        if (query.From is { } from) q = q.Where(b => b.Date >= from);
        if (query.To is { } to) q = q.Where(b => b.Date <= to);
        if (query.PartyId is { } pid) q = q.Where(b => b.PartyId == pid);
        var s = query.NormalizedSearch();
        if (s is not null)
            q = q.Where(b => b.Number.ToLower().Contains(s) || b.Party.Name.ToLower().Contains(s)
                             || (b.SupplierReference != null && b.SupplierReference.ToLower().Contains(s)));
        return q.OrderByDescending(b => b.Date).ThenByDescending(b => b.CreatedAt)
            .Select(b => new DocumentListItem(b.Id, b.Number, b.Date, b.PartyId, b.Party.Name, b.Total, b.Status,
                b.Lines.Count(), b.CreatedBy, b.SupplierReference))
            .ToPagedAsync(query, ct);
    }

    public async Task<PurchaseBillDto> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await read.Query<PurchaseBill>()
                    .Include(x => x.Party)
                    .Include(x => x.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Unit)
                    .Include(x => x.Lines).ThenInclude(l => l.Batch)
                    .Include(x => x.Charges).ThenInclude(c => c.ExpenseCategory)
                    .Include(x => x.Charges).ThenInclude(c => c.PaidFromAccount)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Purchase bill", id);

        var settlements = await LinkedVouchers(DocumentType.PurchaseBill, id, ct);
        var rets = await read.Query<PurchaseReturn>().Where(r => r.PurchaseBillId == id)
            .OrderBy(r => r.Date).Select(r => new ReturnSummaryDto(r.Id, r.Number, r.Date, r.Total)).ToListAsync(ct);

        return new PurchaseBillDto(b.Id, b.Number, b.Date, b.PartyId, b.Party.Code, b.Party.Name, b.SupplierReference, b.Notes,
            b.Status, b.Subtotal, b.ChargesOnParty, b.ChargesTotal, b.Total,
            b.Lines.Select(l => new PurchaseLineDto(l.Id, l.ProductId, l.Product.Name, l.Product.Sku, l.Product.Unit.Symbol,
                l.Quantity, l.UnitCost, l.LineTotal, l.ReturnedQuantity, l.BatchId, l.Batch.BatchNumber, l.Batch.ExpiryDate,
                l.Batch.RemainingQuantity)).ToList(),
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
    public Task<Guid> PostAsync(PurchaseBillRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        if (r.Lines is not { Count: > 0 }) throw new ValidationException("Add at least one line.");
        await system.EnsureOperationalAsync(r.Date, t);

        var party = await parties.GetByIdAsync(r.PartyId, t) ?? throw new NotFoundException("Supplier", r.PartyId);
        var number = await numbers.NextAsync(SequenceKeys.PurchaseBill, t);
        var bill = PurchaseBill.Create(number, r.Date, party, r.SupplierReference, r.Notes);
        var doc = new DocumentRef(DocumentType.PurchaseBill, bill.Id, number);

        var productMap = await LoadProducts(r.Lines.Select(l => l.ProductId), t);
        for (var i = 0; i < r.Lines.Count; i++)
        {
            var line = r.Lines[i];
            var product = productMap[line.ProductId];
            var qty = Quantity.Positive(line.Quantity, $"Line {i + 1} quantity");
            var batch = InventoryBatch.Receive(product, line.BatchNumber, BatchSource.Purchase, bill.Id, r.Date,
                line.ExpiryDate, qty, line.UnitCost, fallbackBatchNumber: $"{number}-{i + 1}");
            batches.Add(batch);
            bill.AddLine(product, batch);
            ledger.StockIn(batch, r.Date, doc, qty);
        }

        foreach (var c in r.Charges ?? Array.Empty<ChargeRequest>())
        {
            var category = await expenseCategories.GetByIdAsync(c.ExpenseCategoryId, t)
                           ?? throw new NotFoundException("Expense category", c.ExpenseCategoryId);
            FinancialAccount? from = null;
            if (c.PaidFromAccountId is { } aid)
                from = await accounts.GetByIdAsync(aid, t) ?? throw new NotFoundException("Account", aid);
            var amount = Money.Of(c.Amount);
            bill.AddCharge(category, c.Description, amount, from);
            if (from is not null)
                ledger.Withdraw(from, r.Date, doc, $"{category.Name} on {number}", amount);
        }

        bill.EnsureHasLines();
        bills.Add(bill);
        ledger.CreditParty(party, r.Date, doc, BuildDescription("Purchase", bill.SupplierReference), Money.Of(bill.Total));

        if (r.Payment is { Amount: > 0 } pay)
        {
            var account = await accounts.GetByIdAsync(pay.AccountId, t) ?? throw new NotFoundException("Account", pay.AccountId);
            var voucher = await poster.PaymentAsync(r.Date, party, account, Money.Of(pay.Amount), pay.Reference, $"Paid on {number}", t);
            voucher.LinkTo(DocumentType.PurchaseBill, bill.Id, number);
        }
        return bill.Id;
    }, ct);

    // ------------------------------------------------------------------ void
    public Task<Guid> VoidAsync(Guid id, VoidRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var bill = await bills.Query()
                       .Include(b => b.Party)
                       .Include(b => b.Lines).ThenInclude(l => l.Batch)
                       .Include(b => b.Lines).ThenInclude(l => l.Product)
                       .Include(b => b.Charges).ThenInclude(c => c.PaidFromAccount)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(b => b.Id == id, t)
                   ?? throw new NotFoundException("Purchase bill", id);

        bill.Void(r.Reason, user.UserName, clock.UtcNow);
        var doc = new DocumentRef(DocumentType.PurchaseBill, bill.Id, bill.Number);
        var note = $"VOID {bill.Number}";

        foreach (var line in bill.Lines)
        {
            if (!line.Batch.IsUntouched)
                throw new DomainException(
                    $"Stock from batch {line.Batch.BatchNumber} ('{line.Product.Name}') has already been sold, returned or written off; void is not possible. Use a purchase return instead.",
                    "batch_in_use");
            var qty = line.Batch.Remaining;
            line.Batch.Consume(qty);
            ledger.StockOut(line.Batch, bill.Date, doc, qty, isReversal: true, note: note);
        }

        foreach (var charge in bill.Charges.Where(c => c.PaidFromAccount is not null))
            ledger.Deposit(charge.PaidFromAccount!, bill.Date, doc, note, Money.Of(charge.Amount), isReversal: true);

        ledger.DebitParty(bill.Party, bill.Date, doc, note, Money.Of(bill.Total), isReversal: true);

        var linked = await vouchers.Query().Include(v => v.Party).Include(v => v.Account).Include(v => v.ToAccount)
            .Where(v => v.LinkedDocumentType == DocumentType.PurchaseBill && v.LinkedDocumentId == id && v.Status == DocumentStatus.Posted)
            .ToListAsync(t);
        foreach (var v in linked)
            poster.Reverse(v, $"{bill.Number} voided: {r.Reason}");

        return id;
    }, ct);

    // ------------------------------------------------------------------ returns
    public Task<PagedResult<ReturnListItem>> ListReturnsAsync(DateRangeQuery query, CancellationToken ct)
    {
        var q = read.Query<PurchaseReturn>();
        if (query.From is { } from) q = q.Where(x => x.Date >= from);
        if (query.To is { } to) q = q.Where(x => x.Date <= to);
        if (query.PartyId is { } pid) q = q.Where(x => x.PartyId == pid);
        var s = query.NormalizedSearch();
        if (s is not null) q = q.Where(x => x.Number.ToLower().Contains(s) || x.PurchaseBill.Number.ToLower().Contains(s) || x.Party.Name.ToLower().Contains(s));
        return q.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt)
            .Select(x => new ReturnListItem(x.Id, x.Number, x.Date, x.PurchaseBillId, x.PurchaseBill.Number, x.PartyId, x.Party.Name,
                x.Total, x.Reason, x.CreatedBy))
            .ToPagedAsync(query, ct);
    }

    public async Task<PurchaseReturnDto> GetReturnAsync(Guid id, CancellationToken ct)
    {
        var x = await read.Query<PurchaseReturn>()
                    .Include(r => r.Party).Include(r => r.PurchaseBill)
                    .Include(r => r.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Unit)
                    .FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("Purchase return", id);
        var refunds = await LinkedVouchers(DocumentType.PurchaseReturn, id, ct);
        return new PurchaseReturnDto(x.Id, x.Number, x.Date, x.PurchaseBillId, x.PurchaseBill.Number, x.PartyId, x.Party.Name,
            x.Reason, x.Total,
            x.Lines.Select(l => new PurchaseReturnLineDto(l.ProductId, l.Product.Name, l.Product.Unit.Symbol, l.Quantity, l.UnitCost, l.Amount)).ToList(),
            refunds, x.CreatedAt, x.CreatedBy);
    }

    public Task<Guid> ReturnAsync(Guid billId, ReturnRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        if (r.Lines is not { Count: > 0 }) throw new ValidationException("Select at least one line to return.");
        await system.EnsureOperationalAsync(r.Date, t);

        var bill = await bills.Query()
                       .Include(b => b.Party)
                       .Include(b => b.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Unit)
                       .Include(b => b.Lines).ThenInclude(l => l.Batch)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(b => b.Id == billId, t)
                   ?? throw new NotFoundException("Purchase bill", billId);

        var number = await numbers.NextAsync(SequenceKeys.PurchaseReturn, t);
        var ret = PurchaseReturn.Create(number, r.Date, bill, r.Reason);
        var doc = new DocumentRef(DocumentType.PurchaseReturn, ret.Id, number);

        foreach (var lr in r.Lines.Where(l => l.Quantity > 0))
        {
            var line = bill.Lines.FirstOrDefault(l => l.Id == lr.BillLineId)
                       ?? throw new ValidationException($"Line {lr.BillLineId} is not on {bill.Number}.");
            var qty = Quantity.Positive(lr.Quantity);
            ret.AddLine(line, qty);
            ledger.StockOut(line.Batch, r.Date, doc, qty, note: $"Returned to supplier ({bill.Number})");
        }
        ret.EnsureHasLines();
        returns.Add(ret);
        ledger.DebitParty(bill.Party, r.Date, doc, $"Purchase return against {bill.Number}", Money.Of(ret.Total));

        if (r.Refund is { Amount: > 0 } refund)
        {
            var account = await accounts.GetByIdAsync(refund.AccountId, t) ?? throw new NotFoundException("Account", refund.AccountId);
            var voucher = await poster.ReceiptAsync(r.Date, bill.Party, account, Money.Of(refund.Amount), refund.Reference,
                $"Refund for {number}", t);
            voucher.LinkTo(DocumentType.PurchaseReturn, ret.Id, number);
        }
        return ret.Id;
    }, ct);

    // ------------------------------------------------------------------ helpers
    private async Task<Dictionary<Guid, Product>> LoadProducts(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var map = await products.Query().Include(p => p.Unit).Where(p => distinct.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var missing = distinct.FirstOrDefault(id => !map.ContainsKey(id));
        if (missing != Guid.Empty) throw new NotFoundException("Product", missing);
        return map;
    }

    private static string BuildDescription(string prefix, string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? prefix : $"{prefix} (ref {reference})";
}
