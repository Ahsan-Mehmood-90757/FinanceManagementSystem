using Fms.Domain.Administration;

namespace Fms.Application.Common;

/// <summary>Write-side repository. Queries are tracked so aggregates can be mutated and saved by the unit of work.</summary>
public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Remove(T entity);
}

/// <summary>Read-side (CQRS-lite) access. Every query is AsNoTracking.</summary>
public interface IReadDb
{
    IQueryable<T> Query<T>() where T : class;
}

/// <summary>
/// Runs a unit of work inside a database transaction and commits once. If an optimistic concurrency
/// conflict occurs (e.g. two sales draining the same batch) the whole unit is retried from scratch.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}

public interface IDocumentNumberGenerator
{
    /// <summary>Returns the next formatted number for a sequence key (e.g. "SB" → "SB-000042"). Must be called inside a unit of work.</summary>
    Task<string> NextAsync(string key, CancellationToken ct = default);
}

public interface ICurrentUser
{
    Guid? Id { get; }
    string? UserName { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    /// <summary>Today's date in the business time zone (default Asia/Karachi).</summary>
    DateOnly Today { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(AppUser user);
}

public static class SequenceKeys
{
    public const string PurchaseBill = "PB";
    public const string SaleBill = "SB";
    public const string PurchaseReturn = "PR";
    public const string SaleReturn = "SR";
    public const string Receipt = "RV";
    public const string Payment = "PV";
    public const string Expense = "EV";
    public const string Transfer = "TV";
    public const string StockAdjustment = "SA";
    public const string Party = "PTY";
    public const string Product = "SKU";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [PurchaseBill] = "PB", [SaleBill] = "SB", [PurchaseReturn] = "PR", [SaleReturn] = "SR",
        [Receipt] = "RV", [Payment] = "PV", [Expense] = "EV", [Transfer] = "TV",
        [StockAdjustment] = "SA", [Party] = "P", [Product] = "SKU"
    };
}
