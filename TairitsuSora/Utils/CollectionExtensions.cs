namespace TairitsuSora.Utils;

public static class CollectionExtensions
{
    public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> collection)
    {
        if (collection is IReadOnlyList<T> list) return list;
        return collection.ToList();
    }

    public static string? EmptyAsNull(this string? str) => string.IsNullOrEmpty(str) ? null : str;

    public static string[] SplitByWhitespaces(this string str) =>
        str.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string[] SplitByWhitespaces(this string str, int count) =>
        str.Split((char[]?)null, count, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static int IndexOf<T>(this T[] array, T value) => Array.IndexOf(array, value);

    public static T ElementAtOrDefault<T>(this ReadOnlySpan<T> span, int index, T def = default)
        where T : struct =>
        index >= span.Length ? def : span[index];

    public static T? ElementAtOrNull<T>(this ReadOnlySpan<T> span, int index)
        where T : struct =>
        index >= span.Length ? null : span[index];

    public static void Shuffle<T>(this IList<T> list) => list.Shuffle(Random.Shared);

    public static void Shuffle<T>(this IList<T> list, Random random)
    {
        int n = list.Count;
        while (n-- > 1)
        {
            int i = random.Next(n + 1);
            (list[i], list[n]) = (list[n], list[i]);
        }
    }

    public static T Sample<T>(this IList<T> list) => list[Random.Shared.Next(list.Count)];
    public static T Sample<T>(this IList<T> list, Random random) => list[random.Next(list.Count)];

    public static int? ParseConsumeLeadingPositiveInt(this ref ReadOnlySpan<char> span)
    {
        int i = 0;
        for (; i < span.Length; i++)
            if (span[i] is < '0' or > '9')
                break;
        if (i == 0) return null;
        if (!int.TryParse(span[..i], out int res)) return null;
        span = span[i..];
        return res;
    }

    public static bool ConsumeIfStartsWith(this ref ReadOnlySpan<char> span, ReadOnlySpan<char> other)
    {
        if (!span.StartsWith(other)) return false;
        span = span[other.Length..];
        return true;
    }

    public static bool ConsumeIfStartsWith(this ref ReadOnlySpan<char> span, char other)
    {
        if (span.IsEmpty || span[0] != other) return false;
        span = span[1..];
        return true;
    }

    public static IDisposable Disposer(this IEnumerable<IDisposable> disposables)
        => new CollectionDisposer { Disposables = disposables };

    private readonly struct CollectionDisposer : IDisposable
    {
        public required IEnumerable<IDisposable> Disposables { get; init; }

        public void Dispose()
        {
            foreach (var d in Disposables)
                d.Dispose();
        }
    }
}
