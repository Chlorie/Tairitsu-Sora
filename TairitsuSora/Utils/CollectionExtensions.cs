using System.Collections.Concurrent;
using LanguageExt;

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

    public static T Sample<T>(this IReadOnlyList<T> list) => list[Random.Shared.Next(list.Count)];
    public static T Sample<T>(this IReadOnlyList<T> list, Random random) => list[random.Next(list.Count)];

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

    public static Option<TValue> TryUpdate<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key,
        Func<TKey, TValue, TValue> updateValueFactory) where TKey : notnull
    {
        while (true)
        {
            if (!dict.TryGetValue(key, out var expected))
                return Option<TValue>.None;
            var newValue = updateValueFactory(key, expected);
            if (dict.TryUpdate(key, newValue, expected))
                return newValue;
        }
    }

    /// <summary>
    /// Update or remove an item in a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    /// <param name="dict">The dictionary.</param>
    /// <param name="key">The key to update or remove.</param>
    /// <param name="updateValueFactory">
    /// If the key exists in the dictionary, this factory is invoked to create the new value for the item. <br/>
    /// If the new value is <see cref="Option{TValue}.None"/>, the item is removed, otherwise it is updated.
    /// </param>
    /// <returns>
    /// If such key does not exist in the dictionary, returns None;
    /// otherwise, returns the value produced by the factory.
    /// </returns>
    public static Option<Option<TValue>> UpdateOrRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key,
        Func<TKey, TValue, Option<TValue>> updateValueFactory) where TKey : notnull
    {
        while (true)
        {
            if (!dict.TryGetValue(key, out var expected))
                return Option<Option<TValue>>.None;
            var updated = updateValueFactory(key, expected);
            if (updated.IsNone)
            {
                if (dict.TryRemove(key, out _))
                    return Option<Option<TValue>>.Some(Option<TValue>.None);
            }
            else
            {
                if (dict.TryUpdate(key, updated.Get(), expected))
                    return updated;
            }
        }
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
