using Fms.Domain.Common;
using Fms.Domain.MasterData;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.Inventory;

/// <summary>
/// A tracked lot of stock received at one cost. FIFO consumption drains the oldest batches first.
/// Protected by an optimistic concurrency token so two concurrent sales can never over-draw a batch.
/// </summary>
public sealed class InventoryBatch : AuditableEntity, IConcurrencyGuarded
{
    private InventoryBatch() { }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public string BatchNumber { get; private set; } = default!;
    public BatchSource Source { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    /// <summary>Cost per unit (4 dp).</summary>
    public decimal UnitCost { get; private set; }
    public decimal OriginalQuantity { get; private set; }
    public decimal RemainingQuantity { get; private set; }
    public uint Version { get; private set; }

    public Quantity Original => Quantity.Of(OriginalQuantity);
    public Quantity Remaining => Quantity.Of(RemainingQuantity);

    public static InventoryBatch Receive(Product product, string? batchNumber, BatchSource source, Guid? sourceDocumentId,
        DateOnly receivedOn, DateOnly? expiryDate, Quantity quantity, decimal unitCost, string fallbackBatchNumber)
    {
        if (!quantity.IsPositive) throw new DomainException("Received quantity must be greater than zero.");
        product.Unit.EnsureValid(quantity, product.Name);
        if (expiryDate is not null && expiryDate < receivedOn)
            throw new DomainException($"Expiry date for '{product.Name}' is before the receipt date.");
        if (product.TracksExpiry && expiryDate is null)
            throw new DomainException($"'{product.Name}' tracks expiry; an expiry date is required.", "expiry_required");

        return new InventoryBatch
        {
            ProductId = product.Id,
            Product = product,
            BatchNumber = Guard.Optional(batchNumber, "Batch number", 50) ?? fallbackBatchNumber,
            Source = source,
            SourceDocumentId = sourceDocumentId,
            ReceivedOn = receivedOn,
            ExpiryDate = expiryDate,
            UnitCost = decimal.Round(Guard.NonNegative(unitCost, "Unit cost"), 4),
            OriginalQuantity = quantity.Value,
            RemainingQuantity = quantity.Value
        };
    }

    public bool IsDepleted => RemainingQuantity == 0m;
    public bool IsUntouched => RemainingQuantity == OriginalQuantity;
    public bool IsExpiredOn(DateOnly date) => ExpiryDate is not null && ExpiryDate.Value < date;

    public void Consume(Quantity quantity)
    {
        if (!quantity.IsPositive) throw new DomainException("Consumed quantity must be greater than zero.");
        if (quantity > Remaining)
            throw new DomainException(
                $"Batch {BatchNumber} has only {Remaining} remaining; cannot take {quantity}.", "batch_insufficient");
        RemainingQuantity = (Remaining - quantity).Value;
    }

    public void Restore(Quantity quantity)
    {
        if (!quantity.IsPositive) throw new DomainException("Restored quantity must be greater than zero.");
        if (Remaining + quantity > Original)
            throw new DomainException($"Batch {BatchNumber} cannot hold more than its original quantity.", "batch_overflow");
        RemainingQuantity = (Remaining + quantity).Value;
    }
}
