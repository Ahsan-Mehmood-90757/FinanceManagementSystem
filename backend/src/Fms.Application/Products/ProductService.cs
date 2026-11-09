using Fms.Application.Common;
using Fms.Domain.Inventory;
using Fms.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Products;

public sealed record ProductDto(
    Guid Id, string Sku, string Name, string? Description,
    Guid? CategoryId, string? CategoryName, Guid UnitId, string UnitSymbol, bool AllowsFractions,
    decimal DefaultSalePrice, decimal ReorderLevel, bool TracksExpiry, bool IsActive,
    decimal StockOnHand, decimal StockValue);

public sealed record ProductRequest(
    string? Sku, string Name, string? Description, Guid? CategoryId, Guid UnitId,
    decimal DefaultSalePrice, decimal ReorderLevel, bool TracksExpiry, bool IsActive = true);

public sealed record ProductQuery : PageQuery
{
    public Guid? CategoryId { get; init; }
    public bool ActiveOnly { get; init; }
    public bool LowStockOnly { get; init; }
}

public sealed class ProductService(
    IRepository<Product> products,
    IRepository<Category> categories,
    IRepository<UnitOfMeasure> units,
    IDocumentNumberGenerator numbers,
    IReadDb read,
    IUnitOfWork uow)
{
    private IQueryable<ProductDto> Project(IQueryable<Product> q) =>
        q.Select(p => new ProductDto(
            p.Id, p.Sku, p.Name, p.Description,
            p.CategoryId,
            p.Category == null ? null : (p.Category.Parent != null ? p.Category.Parent.Name + " / " + p.Category.Name : p.Category.Name),
            p.UnitId, p.Unit.Symbol, p.Unit.AllowsFractions,
            p.DefaultSalePrice, p.ReorderLevel, p.TracksExpiry, p.IsActive,
            read.Query<InventoryBatch>().Where(b => b.ProductId == p.Id).Sum(b => (decimal?)b.RemainingQuantity) ?? 0m,
            read.Query<InventoryBatch>().Where(b => b.ProductId == p.Id).Sum(b => (decimal?)(b.RemainingQuantity * b.UnitCost)) ?? 0m));

    public Task<PagedResult<ProductDto>> ListAsync(ProductQuery query, CancellationToken ct)
    {
        var q = read.Query<Product>();
        var s = query.NormalizedSearch();
        if (s is not null) q = q.Where(p => p.Name.ToLower().Contains(s) || p.Sku.ToLower().Contains(s));
        if (query.CategoryId is { } cat) q = q.Where(p => p.CategoryId == cat || (p.Category != null && p.Category.ParentId == cat));
        if (query.ActiveOnly) q = q.Where(p => p.IsActive);
        if (query.LowStockOnly)
            q = q.Where(p => p.IsActive &&
                (read.Query<InventoryBatch>().Where(b => b.ProductId == p.Id).Sum(b => (decimal?)b.RemainingQuantity) ?? 0m) <= p.ReorderLevel);
        return Project(q.OrderBy(p => p.Name)).ToPagedAsync(query, ct);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(read.Query<Product>().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);

    public Task<Guid> CreateAsync(ProductRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var sku = string.IsNullOrWhiteSpace(r.Sku) ? await numbers.NextAsync(SequenceKeys.Product, t) : r.Sku;
        await EnsureUniqueSku(sku, null, t);
        var (category, unit) = await LoadRefs(r, t);
        var product = Product.Create(sku, r.Name, r.Description, category, unit, r.DefaultSalePrice, r.ReorderLevel, r.TracksExpiry);
        products.Add(product);
        return product.Id;
    }, ct);

    public Task<Guid> UpdateAsync(Guid id, ProductRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var product = await products.Query().Include(p => p.Unit).FirstOrDefaultAsync(p => p.Id == id, t)
                      ?? throw new NotFoundException("Product", id);
        var sku = string.IsNullOrWhiteSpace(r.Sku) ? product.Sku : r.Sku;
        await EnsureUniqueSku(sku, id, t);
        var (category, unit) = await LoadRefs(r, t);
        if (unit.Id != product.UnitId && await read.Query<InventoryBatch>().AnyAsync(b => b.ProductId == id, t))
            throw new ConflictException("The unit of a product with stock history cannot be changed.");
        if (r.TracksExpiry && !product.TracksExpiry &&
            await read.Query<InventoryBatch>().AnyAsync(b => b.ProductId == id && b.RemainingQuantity > 0 && b.ExpiryDate == null, t))
            throw new ConflictException("Existing stock has no expiry dates; expiry tracking cannot be switched on until it is cleared.");
        product.Update(sku, r.Name, r.Description, category, unit, r.DefaultSalePrice, r.ReorderLevel, r.TracksExpiry, r.IsActive);
        return id;
    }, ct);

    private async Task<(Category?, UnitOfMeasure)> LoadRefs(ProductRequest r, CancellationToken ct)
    {
        var unit = await units.GetByIdAsync(r.UnitId, ct) ?? throw new NotFoundException("Unit", r.UnitId);
        Category? category = null;
        if (r.CategoryId is { } cid)
            category = await categories.GetByIdAsync(cid, ct) ?? throw new NotFoundException("Category", cid);
        return (category, unit);
    }

    private async Task EnsureUniqueSku(string sku, Guid? exceptId, CancellationToken ct)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        if (await read.Query<Product>().AnyAsync(p => p.Sku == normalized && p.Id != exceptId, ct))
            throw new ConflictException($"SKU '{normalized}' is already used by another product.");
    }
}
