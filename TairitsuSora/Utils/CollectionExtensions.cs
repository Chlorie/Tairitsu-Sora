namespace TairitsuSora.Utils;

public static class CollectionExtensions
{
    public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> collection)
    {
        if (collection is IReadOnlyList<T> list) return list;
        return collection.ToList();
    }

    public static string[] SplitByWhitespaces(this string str) =>
        str.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string[] SplitByWhitespaces(this string str, int count) =>
        str.Split((char[]?)null, count, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
}
