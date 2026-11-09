using Fms.Domain.Common;

namespace Fms.Domain.ValueObjects;

/// <summary>
/// A stock quantity with up to 4 decimal places so fractional units (e.g. 0.3 L) are exact.
/// </summary>
public readonly record struct Quantity : IComparable<Quantity>
{
    public const int Scale = 4;

    public decimal Value { get; }

    private Quantity(decimal value) => Value = decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

    public static Quantity Zero => new(0m);
    public static Quantity Of(decimal value) => new(value);

    public static Quantity Positive(decimal value, string field = "Quantity")
    {
        var q = new Quantity(value);
        if (q.Value <= 0m) throw new DomainException($"{field} must be greater than zero.", "not_positive");
        return q;
    }

    public bool IsZero => Value == 0m;
    public bool IsPositive => Value > 0m;
    public bool IsWhole => Value == decimal.Truncate(Value);

    public static Quantity operator +(Quantity a, Quantity b) => new(a.Value + b.Value);
    public static Quantity operator -(Quantity a, Quantity b) => new(a.Value - b.Value);
    public static bool operator >(Quantity a, Quantity b) => a.Value > b.Value;
    public static bool operator <(Quantity a, Quantity b) => a.Value < b.Value;
    public static bool operator >=(Quantity a, Quantity b) => a.Value >= b.Value;
    public static bool operator <=(Quantity a, Quantity b) => a.Value <= b.Value;

    public static Quantity Min(Quantity a, Quantity b) => a <= b ? a : b;
    public static Quantity Sum(IEnumerable<Quantity> values) => values.Aggregate(Zero, (acc, q) => acc + q);

    public int CompareTo(Quantity other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString("0.####");
}
