using Fms.Domain.Common;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Inventory;

public readonly record struct BatchAllocation(InventoryBatch Batch, Quantity Quantity)
{
    public Money Cost => Money.FromRate(Quantity, Batch.UnitCost);
}

/// <summary>A user-chosen batch and quantity (manual override, e.g. to clear stock close to expiry).</summary>
public readonly record struct ManualBatchPick(Guid BatchId, Quantity Quantity);

/// <summary>
/// Pure domain service that decides which batches a sale draws from. It does not mutate batches;
/// callers apply the result with <see cref="InventoryBatch.Consume"/>.
/// </summary>
public static class FifoAllocator
{
    /// <summary>Strict chronological order: oldest receipt first, then creation time, then id (UUID v7 is time-ordered).</summary>
    public static IOrderedEnumerable<InventoryBatch> InFifoOrder(IEnumerable<InventoryBatch> batches) =>
        batches.OrderBy(b => b.ReceivedOn).ThenBy(b => b.CreatedAt).ThenBy(b => b.Id);

    /// <param name="batches">Candidate batches for ONE product.</param>
    /// <param name="required">Total quantity to allocate.</param>
    /// <param name="asOf">Sale date; batches expired before this date are never auto-allocated or manually picked.</param>
    /// <param name="productName">For error messages.</param>
    /// <param name="manualPicks">Optional explicit picks. When supplied they must cover the full quantity exactly.</param>
    public static IReadOnlyList<BatchAllocation> Allocate(
        IEnumerable<InventoryBatch> batches,
        Quantity required,
        DateOnly asOf,
        string productName,
        IReadOnlyCollection<ManualBatchPick>? manualPicks = null)
    {
        if (!required.IsPositive) throw new DomainException("Quantity must be greater than zero.");

        var usable = batches.Where(b => b.Remaining.IsPositive && !b.IsExpiredOn(asOf)).ToList();

        if (manualPicks is { Count: > 0 })
            return AllocateManual(usable, required, productName, manualPicks);

        var available = Quantity.Sum(usable.Select(b => b.Remaining));
        if (available < required)
            throw new InsufficientStockException(productName, required.Value, available.Value);

        var result = new List<BatchAllocation>();
        var outstanding = required;
        foreach (var batch in InFifoOrder(usable))
        {
            if (outstanding.IsZero) break;
            var take = Quantity.Min(outstanding, batch.Remaining);
            result.Add(new BatchAllocation(batch, take));
            outstanding -= take;
        }
        return result;
    }

    private static IReadOnlyList<BatchAllocation> AllocateManual(
        List<InventoryBatch> usable, Quantity required, string productName, IReadOnlyCollection<ManualBatchPick> picks)
    {
        if (picks.GroupBy(p => p.BatchId).Any(g => g.Count() > 1))
            throw new DomainException($"The same batch is picked more than once for '{productName}'.", "duplicate_batch_pick");

        var total = Quantity.Sum(picks.Select(p => p.Quantity));
        if (total != required)
            throw new DomainException(
                $"Manual batch picks for '{productName}' total {total} but the line quantity is {required}.", "batch_pick_mismatch");

        var byId = usable.ToDictionary(b => b.Id);
        var result = new List<BatchAllocation>();
        foreach (var pick in picks)
        {
            if (!pick.Quantity.IsPositive)
                throw new DomainException("Picked quantity must be greater than zero.");
            if (!byId.TryGetValue(pick.BatchId, out var batch))
                throw new DomainException(
                    $"Selected batch for '{productName}' is not available (depleted, expired or belongs to another product).", "batch_unavailable");
            if (pick.Quantity > batch.Remaining)
                throw new InsufficientStockException($"{productName} (batch {batch.BatchNumber})", pick.Quantity.Value, batch.RemainingQuantity);
            result.Add(new BatchAllocation(batch, pick.Quantity));
        }
        return result;
    }
}
