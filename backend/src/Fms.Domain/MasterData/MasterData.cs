using Fms.Domain.Common;
using Fms.Domain.ValueObjects;

namespace Fms.Domain.MasterData;

public sealed class UnitOfMeasure : AuditableEntity
{
    private UnitOfMeasure() { }

    public string Name { get; private set; } = default!;
    public string Symbol { get; private set; } = default!;
    /// <summary>When false, quantities in this unit must be whole numbers (e.g. "pcs").</summary>
    public bool AllowsFractions { get; private set; }

    public static UnitOfMeasure Create(string name, string symbol, bool allowsFractions)
    {
        var unit = new UnitOfMeasure();
        unit.Update(name, symbol, allowsFractions);
        return unit;
    }

    public void Update(string name, string symbol, bool allowsFractions)
    {
        Name = Guard.Required(name, "Unit name", 50);
        Symbol = Guard.Required(symbol, "Unit symbol", 15);
        AllowsFractions = allowsFractions;
    }

    public void EnsureValid(Quantity quantity, string productName)
    {
        if (!AllowsFractions && !quantity.IsWhole)
            throw new DomainException(
                $"'{productName}' is measured in {Symbol}, which does not allow fractional quantities.", "fraction_not_allowed");
    }
}

public sealed class Category : AuditableEntity
{
    private Category() { }

    public string Name { get; private set; } = default!;
    public Guid? ParentId { get; private set; }
    public Category? Parent { get; private set; }

    public static Category Create(string name, Category? parent)
    {
        var c = new Category();
        c.Update(name, parent);
        return c;
    }

    public void Update(string name, Category? parent)
    {
        if (parent is not null)
        {
            if (parent.ParentId is not null)
                throw new DomainException("Subcategories can only be one level deep.", "category_depth");
            if (parent.Id == Id)
                throw new DomainException("A category cannot be its own parent.", "category_cycle");
        }
        Name = Guard.Required(name, "Category name", 100);
        ParentId = parent?.Id;
    }
}

public sealed class ExpenseCategory : AuditableEntity
{
    private ExpenseCategory() { }

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static ExpenseCategory Create(string name, string? description)
    {
        var e = new ExpenseCategory();
        e.Update(name, description, true);
        return e;
    }

    public void Update(string name, string? description, bool isActive)
    {
        Name = Guard.Required(name, "Expense category name", 100);
        Description = Guard.Optional(description, "Description");
        IsActive = isActive;
    }
}

public sealed class Product : AuditableEntity
{
    private Product() { }

    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public Guid UnitId { get; private set; }
    public UnitOfMeasure Unit { get; private set; } = default!;
    /// <summary>Suggested selling price per unit (can be overridden on each sale line).</summary>
    public decimal DefaultSalePrice { get; private set; }
    public decimal ReorderLevel { get; private set; }
    public bool TracksExpiry { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Product Create(string sku, string name, string? description, Category? category, UnitOfMeasure unit,
        decimal defaultSalePrice, decimal reorderLevel, bool tracksExpiry)
    {
        var p = new Product();
        p.Update(sku, name, description, category, unit, defaultSalePrice, reorderLevel, tracksExpiry, true);
        return p;
    }

    public void Update(string sku, string name, string? description, Category? category, UnitOfMeasure unit,
        decimal defaultSalePrice, decimal reorderLevel, bool tracksExpiry, bool isActive)
    {
        Sku = Guard.Required(sku, "SKU", 50).ToUpperInvariant();
        Name = Guard.Required(name, "Product name", 200);
        Description = Guard.Optional(description, "Description", 1000);
        CategoryId = category?.Id;
        Category = category;
        UnitId = unit.Id;
        Unit = unit;
        DefaultSalePrice = Guard.NonNegative(decimal.Round(defaultSalePrice, 4), "Default sale price");
        ReorderLevel = Quantity.Of(Guard.NonNegative(reorderLevel, "Reorder level")).Value;
        TracksExpiry = tracksExpiry;
        IsActive = isActive;
    }

    public void EnsureSellable()
    {
        if (!IsActive) throw new DomainException($"Product '{Name}' is inactive.", "inactive_product");
    }
}
