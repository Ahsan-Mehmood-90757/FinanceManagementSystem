namespace Fms.Domain.Common;

/// <summary>A business rule was violated. Mapped to HTTP 422 by the API.</summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "domain_rule") : base(message)
    {
        Code = code;
    }
}

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(string productName, decimal requested, decimal available)
        : base($"Insufficient stock for '{productName}': requested {requested:0.####}, available {available:0.####}.", "insufficient_stock")
    {
        Requested = requested;
        Available = available;
    }

    public decimal Requested { get; }
    public decimal Available { get; }
}

public static class Guard
{
    public static string Required(string? value, string field, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} is required.", "required");
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters.", "too_long");
        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters.", "too_long");
        return trimmed;
    }

    public static decimal NonNegative(decimal value, string field)
    {
        if (value < 0) throw new DomainException($"{field} cannot be negative.", "negative");
        return value;
    }

    public static decimal Positive(decimal value, string field)
    {
        if (value <= 0) throw new DomainException($"{field} must be greater than zero.", "not_positive");
        return value;
    }
}
