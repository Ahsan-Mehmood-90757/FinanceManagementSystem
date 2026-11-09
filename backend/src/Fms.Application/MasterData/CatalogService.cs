using Fms.Application.Common;
using Fms.Domain.Documents;
using Fms.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.MasterData;

public sealed record UnitDto(Guid Id, string Name, string Symbol, bool AllowsFractions, int ProductCount);
public sealed record UnitRequest(string Name, string Symbol, bool AllowsFractions);

public sealed record CategoryDto(Guid Id, string Name, Guid? ParentId, string? ParentName, int ProductCount);
public sealed record CategoryRequest(string Name, Guid? ParentId);

public sealed record ExpenseCategoryDto(Guid Id, string Name, string? Description, bool IsActive);
public sealed record ExpenseCategoryRequest(string Name, string? Description, bool IsActive = true);

/// <summary>Units of measure, product categories / subcategories and expense categories.</summary>
public sealed class CatalogService(
    IRepository<UnitOfMeasure> units,
    IRepository<Category> categories,
    IRepository<ExpenseCategory> expenseCategories,
    IReadDb read,
    IUnitOfWork uow)
{
    // ---------- Units ----------
    public Task<List<UnitDto>> ListUnitsAsync(CancellationToken ct) =>
        read.Query<UnitOfMeasure>()
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Name, u.Symbol, u.AllowsFractions,
                read.Query<Product>().Count(p => p.UnitId == u.Id)))
            .ToListAsync(ct);

    public Task<Guid> CreateUnitAsync(UnitRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        await EnsureUniqueUnit(r.Name, null, t);
        var unit = UnitOfMeasure.Create(r.Name, r.Symbol, r.AllowsFractions);
        units.Add(unit);
        return unit.Id;
    }, ct);

    public Task<Guid> UpdateUnitAsync(Guid id, UnitRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var unit = await units.GetByIdAsync(id, t) ?? throw new NotFoundException("Unit", id);
        await EnsureUniqueUnit(r.Name, id, t);
        if (unit.AllowsFractions && !r.AllowsFractions)
        {
            var hasFractionalStock = await read.Query<Domain.Inventory.InventoryBatch>()
                .AnyAsync(b => b.Product.UnitId == id && b.RemainingQuantity != Math.Floor(b.RemainingQuantity), t);
            if (hasFractionalStock)
                throw new ConflictException("Products in this unit hold fractional stock; fractions cannot be disabled.");
        }
        unit.Update(r.Name, r.Symbol, r.AllowsFractions);
        return unit.Id;
    }, ct);

    public Task<Guid> DeleteUnitAsync(Guid id, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var unit = await units.GetByIdAsync(id, t) ?? throw new NotFoundException("Unit", id);
        if (await read.Query<Product>().AnyAsync(p => p.UnitId == id, t))
            throw new ConflictException($"Unit '{unit.Name}' is used by products and cannot be deleted.");
        units.Remove(unit);
        return id;
    }, ct);

    private async Task EnsureUniqueUnit(string name, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim().ToLower();
        if (await read.Query<UnitOfMeasure>().AnyAsync(u => u.Name.ToLower() == n && u.Id != exceptId, ct))
            throw new ConflictException($"A unit named '{name.Trim()}' already exists.");
    }

    // ---------- Categories ----------
    public Task<List<CategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        read.Query<Category>()
            .OrderBy(c => c.Parent != null ? c.Parent.Name : c.Name)
            .ThenBy(c => c.ParentId != null)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ParentId, c.Parent != null ? c.Parent.Name : null,
                read.Query<Product>().Count(p => p.CategoryId == c.Id)))
            .ToListAsync(ct);

    public Task<Guid> CreateCategoryAsync(CategoryRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var parent = await LoadParent(r.ParentId, t);
        await EnsureUniqueCategory(r.Name, r.ParentId, null, t);
        var category = Category.Create(r.Name, parent);
        categories.Add(category);
        return category.Id;
    }, ct);

    public Task<Guid> UpdateCategoryAsync(Guid id, CategoryRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var category = await categories.GetByIdAsync(id, t) ?? throw new NotFoundException("Category", id);
        var parent = await LoadParent(r.ParentId, t);
        if (parent is not null && await read.Query<Category>().AnyAsync(c => c.ParentId == id, t))
            throw new ConflictException("A category with subcategories cannot itself become a subcategory.");
        await EnsureUniqueCategory(r.Name, r.ParentId, id, t);
        category.Update(r.Name, parent);
        return id;
    }, ct);

    public Task<Guid> DeleteCategoryAsync(Guid id, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var category = await categories.GetByIdAsync(id, t) ?? throw new NotFoundException("Category", id);
        if (await read.Query<Category>().AnyAsync(c => c.ParentId == id, t))
            throw new ConflictException("Delete or move the subcategories first.");
        if (await read.Query<Product>().AnyAsync(p => p.CategoryId == id, t))
            throw new ConflictException($"Category '{category.Name}' is used by products and cannot be deleted.");
        categories.Remove(category);
        return id;
    }, ct);

    private async Task<Category?> LoadParent(Guid? parentId, CancellationToken ct) =>
        parentId is null ? null
            : await categories.GetByIdAsync(parentId.Value, ct) ?? throw new NotFoundException("Parent category", parentId.Value);

    private async Task EnsureUniqueCategory(string name, Guid? parentId, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim().ToLower();
        if (await read.Query<Category>().AnyAsync(c => c.Name.ToLower() == n && c.ParentId == parentId && c.Id != exceptId, ct))
            throw new ConflictException($"Category '{name.Trim()}' already exists at this level.");
    }

    // ---------- Expense categories ----------
    public Task<List<ExpenseCategoryDto>> ListExpenseCategoriesAsync(bool activeOnly, CancellationToken ct) =>
        read.Query<ExpenseCategory>()
            .Where(e => !activeOnly || e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new ExpenseCategoryDto(e.Id, e.Name, e.Description, e.IsActive))
            .ToListAsync(ct);

    public Task<Guid> CreateExpenseCategoryAsync(ExpenseCategoryRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        await EnsureUniqueExpense(r.Name, null, t);
        var e = ExpenseCategory.Create(r.Name, r.Description);
        expenseCategories.Add(e);
        return e.Id;
    }, ct);

    public Task<Guid> UpdateExpenseCategoryAsync(Guid id, ExpenseCategoryRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var e = await expenseCategories.GetByIdAsync(id, t) ?? throw new NotFoundException("Expense category", id);
        await EnsureUniqueExpense(r.Name, id, t);
        e.Update(r.Name, r.Description, r.IsActive);
        return id;
    }, ct);

    public Task<Guid> DeleteExpenseCategoryAsync(Guid id, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var e = await expenseCategories.GetByIdAsync(id, t) ?? throw new NotFoundException("Expense category", id);
        var used = await read.Query<BillCharge>().AnyAsync(c => c.ExpenseCategoryId == id, t)
                   || await read.Query<Voucher>().AnyAsync(v => v.ExpenseCategoryId == id, t);
        if (used) throw new ConflictException($"'{e.Name}' has posted expenses; deactivate it instead of deleting.");
        expenseCategories.Remove(e);
        return id;
    }, ct);

    private async Task EnsureUniqueExpense(string name, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim().ToLower();
        if (await read.Query<ExpenseCategory>().AnyAsync(e => e.Name.ToLower() == n && e.Id != exceptId, ct))
            throw new ConflictException($"Expense category '{name.Trim()}' already exists.");
    }
}
