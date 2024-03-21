namespace TairitsuSora.Utils;

/// <summary>
/// Wraps a nullable object of any type.
/// </summary>
/// <param name="value">The object.</param>
public readonly struct Any(object? value)
{
    public object? Value => value;
    public static readonly Any Null = new(null);
}

public static class AnyExtensions
{
    public static Any ToAny(this object? value) => new(value);
}
