using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Documents;
using Fms.Domain.Inventory;
using Fms.Domain.Ledger;
using Fms.Domain.MasterData;
using Fms.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Inventory;

public sealed record BatchDto(
    Guid Id, Guid ProductId, string ProductName, string Sku, string UnitSymbol, string BatchNumber, BatchSource Source,
    DateOnly ReceivedOn, DateOnly? ExpiryDate, decimal UnitCost, decimal OriginalQuantity, decimal RemainingQuantity,
    decimal RemainingValue);

public sealed record MovementDto(
    Guid Id, DateOnly Date, DocumentType DocumentType, Guid DocumentId, string DocumentNumber, string BatchNumber,
    decimal QuantityIn, decimal QuantityOut, decimal UnitCost, bool IsReversal, string? Note);

public sealed record FifoPreviewLine(Guid BatchId, string BatchNumber, DateOnly ReceivedOn, DateOnly? ExpiryDate,
    decimal Quantity, decimal UnitCost, decimal Cost);

public sealed record FifoPreviewDto(Guid ProductId, decimal Requested, decimal Available, bool Sufficient,
    decimal TotalCost, IReadOnlyList<FifoPreviewLine> Allocations);

public sealed record BatchQuery : PageQuery
{
    public Guid? ProductId { get; init; }
    public bool IncludeDepleted { get; init; }
    /// <summary>Only batches expiring within this many days (expired ones included).</summary>
    public int? ExpiringWithinDays { get; init; }
}

public sealed record StockAdjustmentRequest(DateOnly Date, Guid BatchId, decimal Quantity, string Reason);

public sealed record StockAdjustmentDto(Guid Id, string Number, DateOnly Date, Guid ProductId, string ProductName,
    string BatchNumber, decimal Quantity, decimal UnitCost, decimal Value, string Reason, string? CreatedBy);

public sealed class InventoryService(
    IRepository<InventoryBatch> batches,
    IRepository<StockAdjustment> adjustments,
    LedgerWriter ledger,
    SystemState system,
    IDocumentNumberGenerator numbers,
    IClock clock,
    IReadDb read,
    IUnitOfWork uow)
{
    public Task<PagedResult<BatchDto>> ListBatchesAsync(BatchQuery query, CancellationToken ct)
    {
        var q = read.Query<InventoryBatch>();
        if (query.ProductId is { } pid) q = q.Where(b => b.ProductId == pid);
        if (!query.IncludeDepleted) q = q.Where(b => b.RemainingQuantity > 0);
        if (query.ExpiringWithinDays is { } days)
        {
            var limit = clock.Today.AddDays(days);
            q = q.Where(b => b.ExpiryDate != null && b.ExpiryDate <= limit);
        }
        var s = query.NormalizedSearch();
        if (s is not null)
            q = q.Where(b => b.BatchNumber.ToLower().Contains(s) || b.Product.Name.ToLower().Contains(s) || b.Product.Sku.ToLower().Contains(s));

        return q.OrderBy(b => b.ExpiryDate == null).ThenBy(b => b.ExpiryDate).ThenBy(b => b.ReceivedOn).ThenBy(b => b.CreatedAt)
            .Select(b => new BatchDto(b.Id, b.ProductId, b.Product.Name, b.Product.Sku, b.Product.Unit.Symbol, b.BatchNumber, b.Source,
                b.ReceivedOn, b.ExpiryDate, b.UnitCost, b.OriginalQuantity, b.RemainingQuantity, b.RemainingQuantity * b.UnitCost))
            .ToPagedAsync(query, ct);
    }

    public Task<PagedResult<MovementDto>> ListMovementsAsync(Guid productId, PageQuery page, CancellationToken ct) =>
        read.Query<InventoryMovement>()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.CreatedAt)
            .Select(m => new MovementDto(m.Id, m.Date, m.DocumentType, m.DocumentId, m.DocumentNumber,
                read.Query<InventoryBatch>().Where(b => b.Id == m.BatchId).Select(b => b.BatchNumber).FirstOrDefault() ?? "",
                m.QuantityIn, m.QuantityOut, m.UnitCost, m.IsReversal, m.Note))
            .ToPagedAsync(page, ct);

    /// <summary>Shows which batches a sale of <paramref name="quantity"/> would draw from, without changing anything.</summary>
    public async Task<FifoPreviewDto> PreviewFifoAsync(Guid productId, decimal quantity, DateOnly? asOf, CancellationToken ct)
    {
        var product = await read.Query<Product>().FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        var date = asOf ?? clock.Today;
        var candidates = await read.Query<InventoryBatch>()
            .Where(b => b.ProductId == productId && b.RemainingQuantity > 0)
            .ToListAsync(ct);
        var usable = candidates.Where(b => !b.IsExpiredOn(date)).ToList();
        var available = usable.Sum(b => b.RemainingQuantity);
        var requested = Quantity.Of(quantity);
        if (!requested.IsPositive || available < requested.Value)
            return new FifoPreviewDto(productId, requested.Value, available, false, 0m, []);

        var allocations = FifoAllocator.Allocate(usable, requested, date, product.Name);
        var lines = allocations.Select(a => new FifoPreviewLine(a.Batch.Id, a.Batch.BatchNumber, a.Batch.ReceivedOn,
            a.Batch.ExpiryDate, a.Quantity.Value, a.Batch.UnitCost, a.Cost.Amount)).ToList();
        return new FifoPreviewDto(productId, requested.Value, available, true, lines.Sum(l => l.Cost), lines);
    }

    // ---------- Stock write-offs ----------
    public Task<PagedResult<StockAdjustmentDto>> ListAdjustmentsAsync(DateRangeQuery query, CancellationToken ct)
    {
        var q = read.Query<StockAdjustment>();
        if (query.From is { } from) q = q.Where(a => a.Date >= from);
        if (query.To is { } to) q = q.Where(a => a.Date <= to);
        var s = query.NormalizedSearch();
        if (s is not null) q = q.Where(a => a.Number.ToLower().Contains(s) || a.Product.Name.ToLower().Contains(s));
        return q.OrderByDescending(a => a.Date).ThenByDescending(a => a.CreatedAt)
            .Select(a => new StockAdjustmentDto(a.Id, a.Number, a.Date, a.ProductId, a.Product.Name, a.Batch.BatchNumber,
                a.Quantity, a.UnitCost, a.Value, a.Reason, a.CreatedBy))
            .ToPagedAsync(query, ct);
    }

    public Task<Guid> WriteOffAsync(StockAdjustmentRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        await system.EnsureOperationalAsync(r.Date, t);
        var batch = await batches.Query()
                        .Include(b => b.Product).ThenInclude(p => p.Unit)
                        .FirstOrDefaultAsync(b => b.Id == r.BatchId, t)
                    ?? throw new NotFoundException("Batch", r.BatchId);
        if (r.Date < batch.ReceivedOn)
            throw new ValidationException("Write-off date cannot be before the batch was received.");

        var number = await numbers.NextAsync(SequenceKeys.StockAdjustment, t);
        var adjustment = StockAdjustment.WriteOff(number, r.Date, batch, Quantity.Positive(r.Quantity), r.Reason);
        adjustments.Add(adjustment);
        ledger.StockOut(batch, r.Date, new DocumentRef(DocumentType.StockAdjustment, adjustment.Id, number),
            Quantity.Of(adjustment.Quantity), note: adjustment.Reason);
        return adjustment.Id;
    }, ct);
}
