using System.Text;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.Concyclic;

public readonly record struct Point(Rational X, Rational Y)
{
    public override string ToString() => $"({X}, {Y})";

    public static Rational SqrDistance(Point a, Point b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}

public interface IGeneralizedCircle : IEquatable<IGeneralizedCircle>
{
    bool Contains(Point point);
    string Equation();
}

public readonly record struct Circle(Point Center, Rational SqrRadius) : IGeneralizedCircle
{
    public bool Contains(Point point) => Point.SqrDistance(point, Center) == SqrRadius;

    public string Equation()
    {
        string Term(char variable, Rational offset) =>
            offset == 0 ? $"{variable}" :
            offset > 0 ? $"({variable} + {offset})"
            : $"({variable} - {-offset})";

        return $"{Term('x', -Center.X)}² + {Term('y', -Center.Y)}² = {SqrRadius}";
    }

    public bool Equals(IGeneralizedCircle? other) => other is Circle circle && circle == this;
}

public readonly record struct Line : IGeneralizedCircle
{
    public Rational XCoef { get; private init; }
    public Rational YCoef { get; private init; }
    public Rational Constant { get; private init; }

    public static Line FromPoints(Point p, Point q)
    {
        Rational xCoef = p.Y - q.Y, yCoef = q.X - p.X, c = p.Y * q.X - p.X * q.Y;
        if (c < 0)
        {
            xCoef = -xCoef;
            yCoef = -yCoef;
            c = -c;
        }
        else if (c == 0 && xCoef < 0)
        {
            xCoef = -xCoef;
            yCoef = -yCoef;
        }
        Rational gcd = Rational.Gcd(Rational.Gcd(xCoef, yCoef), c);
        return new Line { XCoef = xCoef / gcd, YCoef = yCoef / gcd, Constant = c / gcd };
    }

    public bool Contains(Point point) => XCoef * point.X + YCoef * point.Y == Constant;

    public string Equation()
    {
        StringBuilder sb = new();
        if (XCoef != 0)
        {
            if (XCoef == 1)
                sb.Append('x');
            else if (XCoef == -1)
                sb.Append("-x");
            else
                sb.Append($"{XCoef}x");
            if (YCoef == 1)
                sb.Append(" + y");
            else if (YCoef == -1)
                sb.Append(" - y");
            else if (YCoef > 0)
                sb.Append($" + {YCoef}y");
            else if (YCoef < 0)
                sb.Append($" - {-YCoef}y");
        }
        else
        {
            if (YCoef == 1)
                sb.Append('y');
            else if (YCoef == -1)
                sb.Append("-y");
            else
                sb.Append($"{YCoef}y");
        }
        sb.Append($" = {Constant}");
        return sb.ToString();
    }

    public bool Equals(IGeneralizedCircle? other) => other is Line line && line == this;
}

public static class GeneralizedCircle
{
    public static IGeneralizedCircle FromPoints(Point p, Point q, Point r)
    {
        Rational den = Det3(
            p.X, p.Y, 1,
            q.X, q.Y, 1,
            r.X, r.Y, 1
        );
        if (den == 0) // collinear points
            return Line.FromPoints(p, q);
        Rational ps = p.X * p.X + p.Y * p.Y;
        Rational qs = q.X * q.X + q.Y * q.Y;
        Rational rs = r.X * r.X + r.Y * r.Y;
        Rational xNum = Det3(
            ps, p.Y, 1,
            qs, q.Y, 1,
            rs, r.Y, 1
        );
        Rational yNum = Det3(
            ps, p.X, 1,
            qs, q.X, 1,
            rs, r.X, 1
        );
        Point center = new()
        {
            X = xNum / (2 * den),
            Y = -yNum / (2 * den)
        };
        return new Circle(center, Point.SqrDistance(p, center));
    }

    private static Rational Det3(
        Rational m11, Rational m12, Rational m13,
        Rational m21, Rational m22, Rational m23,
        Rational m31, Rational m32, Rational m33
    ) =>
        m11 * m22 * m33 + m12 * m23 * m31 + m13 * m21 * m32 -
        m11 * m23 * m32 - m12 * m21 * m33 - m13 * m22 * m31;
}
