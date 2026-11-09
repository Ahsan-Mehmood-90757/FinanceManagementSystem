namespace Fms.Application.Common;

public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;
}

/// <summary>Request is well-formed but conflicts with existing data (duplicate code, in-use record...). HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Input validation failure. HTTP 400.</summary>
public sealed class ValidationException(string message, IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public IDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

public sealed class UnauthorizedException(string message) : Exception(message);
