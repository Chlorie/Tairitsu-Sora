using System.Globalization;

namespace TairitsuSora.Utils;

public readonly struct Rational() : IFormattable, IEquatable<Rational>, IComparable<Rational>, IComparable
{
    public long Numerator { get; private init; } = 0;
    public long Denominator { get; private init; } = 1;

    public static Rational FromFraction(long numerator, long denominator)
    {
        if (denominator == 0) throw new DivideByZeroException("Denominator cannot be zero in a fraction.");
        long gcd = Gcd(denominator, long.Abs(numerator));
        if (denominator < 0) gcd = -gcd;
        numerator /= gcd;
        denominator /= gcd;
        return new Rational { Numerator = numerator, Denominator = denominator };
    }

    public static implicit operator Rational(long x) => new() { Numerator = x };
    public static explicit operator float(Rational x) => (float)x.Numerator / x.Denominator;

    public static Rational operator +(Rational x) => x;
    public static Rational operator -(Rational x) => new() { Numerator = -x.Numerator, Denominator = x.Denominator };

    public Rational Abs() => new() { Numerator = long.Abs(Numerator), Denominator = Denominator };

    public static Rational operator +(Rational x, Rational y) =>
        FromFraction(x.Numerator * y.Denominator + y.Numerator * x.Denominator, x.Denominator * y.Denominator);
    public static Rational operator -(Rational x, Rational y) =>
        FromFraction(x.Numerator * y.Denominator - y.Numerator * x.Denominator, x.Denominator * y.Denominator);
    public static Rational operator *(Rational x, Rational y) =>
        FromFraction(x.Numerator * y.Numerator, x.Denominator * y.Denominator);
    public static Rational operator /(Rational x, Rational y) =>
        FromFraction(x.Numerator * y.Denominator, x.Denominator * y.Numerator);

    public static Rational Gcd(Rational x, Rational y)
    {
        long num = Gcd(x.Numerator, y.Numerator);
        long den = x.Denominator * y.Denominator / Gcd(x.Denominator, y.Denominator);
        return new Rational { Numerator = num, Denominator = den };
    }

    public override bool Equals(object? obj) =>
        obj is Rational r && r.Numerator == Numerator && r.Denominator == Denominator;

    public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;

    public override int GetHashCode()
    {
        unchecked
        {
            return Numerator.GetHashCode() * 397 ^ Denominator.GetHashCode();
        }
    }

    public static bool operator ==(Rational x, Rational y) => x.Equals(y);
    public static bool operator !=(Rational x, Rational y) => !x.Equals(y);
    public static bool operator <(Rational x, Rational y) => x.Numerator * y.Denominator < y.Numerator * x.Denominator;
    public static bool operator >(Rational x, Rational y) => y < x;
    public static bool operator <=(Rational x, Rational y) => !(y < x);
    public static bool operator >=(Rational x, Rational y) => !(x < y);

    public int CompareTo(Rational other)
    {
        long left = Numerator * other.Denominator, right = other.Numerator * Denominator;
        return left < right ? -1 : left > right ? 1 : 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        return obj is Rational other ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(Rational)}");
    }

    public override string ToString() => ToString("G", CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? provider)
    {
        if (string.IsNullOrEmpty(format)) format = "G";
        string nums = Numerator.ToString(format switch
        {
            "G" or "g" => "",
            "+" => "+",
            _ => throw new FormatException("Invalid format specifier.")
        }, provider ?? CultureInfo.InvariantCulture);
        return Denominator == 1 ? nums : $"{nums}/{Denominator}";
    }

    private static long Gcd(long x, long y)
    {
        x = long.Abs(x);
        y = long.Abs(y);
        while (true)
        {
            if (x > y)
            {
                (x, y) = (y, x);
                continue;
            }
            if (x == 0) return y;
            (x, y) = (y % x, x);
        }
    }
}
