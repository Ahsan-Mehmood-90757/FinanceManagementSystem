using System.Data;
using Fms.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Fms.Infrastructure.Persistence;

public sealed class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>();
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) => await db.Set<T>().FindAsync([id], ct);
    public void Add(T entity) => db.Set<T>().Add(entity);
    public void AddRange(IEnumerable<T> entities) => db.Set<T>().AddRange(entities);
    public void Remove(T entity) => db.Set<T>().Remove(entity);
}

/// <summary>
/// One database transaction per business operation. Optimistic concurrency conflicts (xmin changed under us,
/// e.g. two cashiers selling the last units of a batch) roll back and re-run the whole operation against fresh data.
/// </summary>
public sealed class UnitOfWork(AppDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private const int MaxAttempts = 3;

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        // Nested call (a service calling another service inside the same unit): just run it.
        if (db.Database.CurrentTransaction is not null)
            return await work(ct);

        for (var attempt = 1; ; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                var result = await work(ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Concurrency conflict on attempt {Attempt}; retrying unit of work", attempt);
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(15, 60) * attempt, ct);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
    }
}

/// <summary>Row-locking counter: UPDATE ... RETURNING serialises concurrent callers and rolls back with the transaction (gap-free).</summary>
public sealed class DocumentNumberGenerator(AppDbContext db) : IDocumentNumberGenerator
{
    public async Task<string> NextAsync(string key, CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText =
            "UPDATE document_sequences SET next_value = next_value + 1 WHERE key = @key RETURNING prefix, next_value - 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = key;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException($"Document sequence '{key}' is not configured.");
        var prefix = reader.GetString(0);
        var value = reader.GetInt64(1);
        return $"{prefix}-{value:D6}";
    }
}
