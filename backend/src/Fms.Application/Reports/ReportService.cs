using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Accounts;
using Fms.Domain.Documents;
using Fms.Domain.Inventory;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Reports;

public sealed record AmountByName(string Name, decimal Amount);
public sealed record DailyPoint(DateOnly Date, decimal Sales, decimal GrossProfit);
public sealed record AccountBalance(Guid Id, string Name, AccountType Type, decimal Balance);
public sealed record RecentDocument(Guid Id, string Kind, string Number, DateOnly Date, string? Counterparty, decimal Amount, DocumentStatus Status);
public sealed record TopProduct(Guid ProductId, string Name, decimal Quantity, decimal Revenue, decimal GrossProfit);

public sealed record DashboardDto(
    DateOnly Today, decimal SalesToday, decimal SalesMonth, decimal GrossProfitMonth, decimal ExpensesMonth, decimal NetProfitMonth,
    decimal CashAndBank, decimal Receivables, decimal Payables, decimal StockValue,
    int LowStockCount, int ExpiringSoonCount,
    IReadOnlyList<AccountBalance> Accounts, IReadOnlyList<DailyPoint> Trend,
    IReadOnlyList<RecentDocument> Recent, IReadOnlyList<TopProduct> TopProducts);

public sealed record ProfitLossDto(
    DateOnly From, DateOnly To,
    decimal GrossSales, decimal SalesReturns, decimal NetSales,
    decimal CostOfGoodsSold, decimal CostReversedOnReturns, decimal NetCostOfGoodsSold,
    decimal GrossProfit, decimal GrossMarginPercent,
    IReadOnlyList<AmountByName> BillCharges, decimal BillChargesTotal,
    IReadOnlyList<AmountByName> OperatingExpenses, decimal OperatingExpensesTotal,
    decimal StockWriteOffs, decimal TotalExpenses, decimal NetProfit, decimal NetMarginPercent);

public sealed record ValuationRow(Guid ProductId, string Sku, string Name, string? Category, string UnitSymbol,
    decimal Quantity, decimal Value, decimal AverageCost, DateOnly? EarliestExpiry, int BatchCount);

public sealed record InventoryValuationDto(decimal TotalValue, decimal TotalQuantityLines, IReadOnlyList<ValuationRow> Rows);

public sealed record PartyBalanceRow(Guid Id, string Code, string Name, PartyKind Kind, string? Phone, decimal Balance);

public sealed record BalancesDto(decimal TotalReceivable, decimal TotalPayable,
    IReadOnlyList<PartyBalanceRow> Receivables, IReadOnlyList<PartyBalanceRow> Payables);

public sealed class ReportService(IReadDb read, IClock clock)
{
    public async Task<DashboardDto> DashboardAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var trendStart = today.AddDays(-29);

        var sales = read.Query<SaleBill>().Where(b => b.Status == DocumentStatus.Posted);
        var saleReturns = read.Query<SaleReturn>();

        var salesToday = await sales.Where(b => b.Date == today).SumAsync(b => b.Subtotal, ct)
                         - await saleReturns.Where(r => r.Date == today).SumAsync(r => r.Total, ct);

        var pl = await ProfitLossAsync(monthStart, today, ct);

        var accounts = await read.Query<FinancialAccount>().Where(a => a.IsActive)
            .OrderBy(a => a.Type).ThenBy(a => a.Name)
            .Select(a => new AccountBalance(a.Id, a.Name, a.Type, a.Balance)).ToListAsync(ct);

        var parties = read.Query<Party>();
        var receivables = await parties.Where(p => p.Balance > 0).SumAsync(p => p.Balance, ct);
        var payables = await parties.Where(p => p.Balance < 0).SumAsync(p => -p.Balance, ct);

        var liveBatches = read.Query<InventoryBatch>().Where(b => b.RemainingQuantity > 0);
        var stockValue = await liveBatches.SumAsync(b => b.RemainingQuantity * b.UnitCost, ct);
        var expiryLimit = today.AddDays(30);
        var expiringSoon = await liveBatches.CountAsync(b => b.ExpiryDate != null && b.ExpiryDate <= expiryLimit, ct);
        var lowStock = await read.Query<Product>().CountAsync(p => p.IsActive && p.ReorderLevel > 0 &&
            (read.Query<InventoryBatch>().Where(b => b.ProductId == p.Id).Sum(b => (decimal?)b.RemainingQuantity) ?? 0m) <= p.ReorderLevel, ct);

        // 30-day trend (net of returns).
        var salesByDay = await sales.Where(b => b.Date >= trendStart && b.Date <= today)
            .GroupBy(b => b.Date)
            .Select(g => new { Date = g.Key, Sales = g.Sum(b => b.Subtotal), Cost = g.Sum(b => b.CostOfGoods) })
            .ToListAsync(ct);
        var returnsByDay = await saleReturns.Where(r => r.Date >= trendStart && r.Date <= today)
            .GroupBy(r => r.Date)
            .Select(g => new { Date = g.Key, Sales = g.Sum(r => r.Total), Cost = g.Sum(r => r.CostTotal) })
            .ToListAsync(ct);
        var trend = Enumerable.Range(0, 30).Select(i =>
        {
            var d = trendStart.AddDays(i);
            var s = salesByDay.FirstOrDefault(x => x.Date == d);
            var r = returnsByDay.FirstOrDefault(x => x.Date == d);
            var net = (s?.Sales ?? 0) - (r?.Sales ?? 0);
            var cost = (s?.Cost ?? 0) - (r?.Cost ?? 0);
            return new DailyPoint(d, net, net - cost);
        }).ToList();

        var recentSales = await read.Query<SaleBill>().OrderByDescending(b => b.CreatedAt).Take(6)
            .Select(b => new RecentDocument(b.Id, "sale", b.Number, b.Date, b.Party.Name, b.Total, b.Status)).ToListAsync(ct);
        var recentPurchases = await read.Query<PurchaseBill>().OrderByDescending(b => b.CreatedAt).Take(6)
            .Select(b => new RecentDocument(b.Id, "purchase", b.Number, b.Date, b.Party.Name, b.Total, b.Status)).ToListAsync(ct);
        var recentVouchers = await read.Query<Voucher>().OrderByDescending(v => v.CreatedAt).Take(6)
            .Select(v => new RecentDocument(v.Id, "voucher", v.Number, v.Date,
                v.Party != null ? v.Party.Name : (v.ExpenseCategory != null ? v.ExpenseCategory.Name : (v.ToAccount != null ? v.ToAccount.Name : null)),
                v.Amount, v.Status)).ToListAsync(ct);
        var recent = recentSales.Concat(recentPurchases).Concat(recentVouchers)
            .OrderByDescending(d => d.Date).ThenByDescending(d => d.Number).Take(8).ToList();

        // Group on scalar keys only (EF can't translate navigation members inside a GroupBy key), then attach names.
        var postedThisMonth = read.Query<SaleBill>()
            .Where(b => b.Status == DocumentStatus.Posted && b.Date >= monthStart && b.Date <= today)
            .Select(b => b.Id);
        var topRows = await read.Query<SaleBillLine>()
            .Where(l => postedThisMonth.Contains(l.SaleBillId))
            .GroupBy(l => l.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(l => l.Quantity - l.ReturnedQuantity),
                Revenue = g.Sum(l => l.LineTotal),
                Profit = g.Sum(l => l.LineTotal - l.CostTotal)
            })
            .OrderByDescending(x => x.Revenue).Take(5).ToListAsync(ct);
        var topIds = topRows.Select(x => x.ProductId).ToList();
        var topNames = await read.Query<Product>().Where(p => topIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var top = topRows.Select(x => new TopProduct(x.ProductId, topNames.GetValueOrDefault(x.ProductId, "Unknown product"),
            x.Quantity, x.Revenue, x.Profit)).ToList();

        return new DashboardDto(today, salesToday, pl.NetSales, pl.GrossProfit, pl.TotalExpenses, pl.NetProfit,
            accounts.Sum(a => a.Balance), receivables, payables, stockValue, lowStock, expiringSoon,
            accounts, trend, recent, top);
    }

    public async Task<ProfitLossDto> ProfitLossAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from) throw new ValidationException("'To' date must be on or after 'From' date.");

        var sales = read.Query<SaleBill>().Where(b => b.Status == DocumentStatus.Posted && b.Date >= from && b.Date <= to);
        var grossSales = await sales.SumAsync(b => b.Subtotal, ct);
        var cogs = await sales.SumAsync(b => b.CostOfGoods, ct);

        var returns = read.Query<SaleReturn>().Where(r => r.Date >= from && r.Date <= to);
        var returnsTotal = await returns.SumAsync(r => r.Total, ct);
        var returnsCost = await returns.SumAsync(r => r.CostTotal, ct);

        var netSales = grossSales - returnsTotal;
        var netCogs = cogs - returnsCost;
        var grossProfit = netSales - netCogs;

        // Operational charges attached to posted bills in range (purchase or sale).
        var chargeRows = await read.Query<BillCharge>()
            .Where(c =>
                (c.PurchaseBillId != null && read.Query<PurchaseBill>().Any(b => b.Id == c.PurchaseBillId && b.Status == DocumentStatus.Posted && b.Date >= from && b.Date <= to)) ||
                (c.SaleBillId != null && read.Query<SaleBill>().Any(b => b.Id == c.SaleBillId && b.Status == DocumentStatus.Posted && b.Date >= from && b.Date <= to)))
            .GroupBy(c => c.ExpenseCategoryId)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(c => c.Amount) })
            .ToListAsync(ct);

        var expenseRows = await read.Query<Voucher>()
            .Where(v => v.Type == VoucherType.Expense && v.Status == DocumentStatus.Posted && v.Date >= from && v.Date <= to)
            .Where(v => v.ExpenseCategoryId != null)
            .GroupBy(v => v.ExpenseCategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(v => v.Amount) })
            .ToListAsync(ct);

        var categoryNames = await read.Query<ExpenseCategory>().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var chargeByName = chargeRows.Select(x => new AmountByName(categoryNames.GetValueOrDefault(x.CategoryId, "Other"), x.Amount)).ToList();
        var expenseByName = expenseRows.Select(x => new AmountByName(categoryNames.GetValueOrDefault(x.CategoryId, "Other"), x.Amount)).ToList();

        var writeOffs = await read.Query<StockAdjustment>().Where(a => a.Date >= from && a.Date <= to).SumAsync(a => a.Value, ct);

        var chargesTotal = chargeRows.Sum(x => x.Amount);
        var opexTotal = expenseRows.Sum(x => x.Amount);
        var totalExpenses = chargesTotal + opexTotal + writeOffs;
        var netProfit = grossProfit - totalExpenses;

        return new ProfitLossDto(from, to, grossSales, returnsTotal, netSales, cogs, returnsCost, netCogs,
            grossProfit, Percent(grossProfit, netSales),
            chargeByName.OrderByDescending(x => x.Amount).ToList(), chargesTotal,
            expenseByName.OrderByDescending(x => x.Amount).ToList(), opexTotal,
            writeOffs, totalExpenses, netProfit, Percent(netProfit, netSales));
    }

    public async Task<InventoryValuationDto> InventoryValuationAsync(CancellationToken ct)
    {
        var rows = await read.Query<InventoryBatch>()
            .Where(b => b.RemainingQuantity > 0)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.RemainingQuantity),
                Value = g.Sum(b => b.RemainingQuantity * b.UnitCost),
                EarliestExpiry = g.Min(b => b.ExpiryDate),
                Count = g.Count()
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.ProductId).ToList();
        var products = await read.Query<Product>().Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id, p.Sku, p.Name, Symbol = p.Unit.Symbol,
                Category = p.Category == null ? null : p.Category.Name
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var result = rows
            .Where(x => products.ContainsKey(x.ProductId))
            .Select(x =>
            {
                var p = products[x.ProductId];
                return new ValuationRow(x.ProductId, p.Sku, p.Name, p.Category, p.Symbol, x.Quantity,
                    decimal.Round(x.Value, 2), x.Quantity == 0 ? 0 : decimal.Round(x.Value / x.Quantity, 4), x.EarliestExpiry, x.Count);
            })
            .OrderBy(r => r.Name)
            .ToList();
        return new InventoryValuationDto(result.Sum(r => r.Value), result.Count, result);
    }

    public async Task<BalancesDto> BalancesAsync(CancellationToken ct)
    {
        var q = read.Query<Party>();
        var receivables = await q.Where(p => p.Balance > 0).OrderByDescending(p => p.Balance)
            .Select(p => new PartyBalanceRow(p.Id, p.Code, p.Name, p.Kind, p.Phone, p.Balance)).ToListAsync(ct);
        var payables = await q.Where(p => p.Balance < 0).OrderBy(p => p.Balance)
            .Select(p => new PartyBalanceRow(p.Id, p.Code, p.Name, p.Kind, p.Phone, -p.Balance)).ToListAsync(ct);
        return new BalancesDto(receivables.Sum(r => r.Balance), payables.Sum(p => p.Balance), receivables, payables);
    }

    private static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0 : decimal.Round(part / whole * 100m, 2);
}
