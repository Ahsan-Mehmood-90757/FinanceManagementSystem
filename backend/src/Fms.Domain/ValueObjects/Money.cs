using System.Globalization;

namespace Fms.Domain.ValueObjects;

/// <summary>
/// An amount in Pakistani Rupees, always rounded to 2 decimal places (paisa) using
/// banker-safe AwayFromZero rounding. Immutable; all arithmetic returns a new value.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int Scale = 2;
    public const string Currency = "PKR";

    public decimal Amount { get; }

    private Money(decimal amount) => Amount = decimal.Round(amount, Scale, MidpointRounding.AwayFromZero);

    public static Money Zero => new(0m);
    public static Money Of(decimal amount) => new(amount);

    /// <summary>quantity × unit rate, rounded to paisa.</summary>
    public static Money FromRate(Quantity quantity, decimal unitRate) => new(quantity.Value * unitRate);

    public bool IsZero => Amount == 0m;
    public bool IsNegative => Amount < 0m;
    public bool IsPositive => Amount > 0m;
    public Money Abs() => new(Math.Abs(Amount));

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public static Money operator -(Money a) => new(-a.Amount);
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor);
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;

    public static Money Sum(IEnumerable<Money> values) => values.Aggregate(Zero, (acc, m) => acc + m);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => $"{Currency} {Amount.ToString("N2", CultureInfo.InvariantCulture)}";
}
