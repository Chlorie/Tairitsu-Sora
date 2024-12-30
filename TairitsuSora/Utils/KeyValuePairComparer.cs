namespace TairitsuSora.Utils;

public class KeyValuePairComparer<TKey, TValue> : IEqualityComparer<KeyValuePair<TKey, TValue>>
{
    public static KeyValuePairComparer<TKey, TValue> Instance { get; } = new();
    public bool Equals(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y) =>
        Equals(x.Key, y.Key) && Equals(x.Value, y.Value);
    public int GetHashCode(KeyValuePair<TKey, TValue> obj) => HashCode.Combine(obj.Key, obj.Value);
}
