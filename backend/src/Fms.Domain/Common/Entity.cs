namespace Fms.Domain.Common;

/// <summary>Base type for every persisted entity. Ids are time-ordered (UUID v7) so they index well.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>Entities that record who created / last changed them. Populated by the persistence layer.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}

public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Entities protected by an optimistic concurrency token (PostgreSQL xmin).</summary>
public interface IConcurrencyGuarded
{
    uint Version { get; }
}
