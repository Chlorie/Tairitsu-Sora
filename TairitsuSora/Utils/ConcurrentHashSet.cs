using System.Collections;
using System.Collections.Immutable;

namespace TairitsuSora.Utils;

public class ConcurrentHashSet<T> : ISet<T>, IReadOnlySet<T>
{
    public int Count => _data.Count;
    public bool IsReadOnly => false;

    public ConcurrentHashSet() { }
    public ConcurrentHashSet(IEnumerable<T> collection) => _data = ImmutableHashSet.CreateRange(collection);
    public ConcurrentHashSet(ImmutableHashSet<T> data) => _data = data;

    public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    void ICollection<T>.Add(T item) => Add(item);
    bool ISet<T>.Add(T item) => Add(item);

    public bool Contains(T item) => _data.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<T> other) => _data.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _data.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => _data.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _data.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _data.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _data.SetEquals(other);

#pragma warning disable CS0420 // Interlocked updates respect volatile
    public bool Add(T item)
        => ImmutableInterlocked.Update(ref _data, static (data, item) => data.Add(item), item);
    public bool Remove(T item)
        => ImmutableInterlocked.Update(ref _data, static (data, item) => data.Remove(item), item);
    public void ExceptWith(IEnumerable<T> other)
        => ImmutableInterlocked.Update(ref _data, static (data, other) => data.Except(other), other);
    public void IntersectWith(IEnumerable<T> other)
        => ImmutableInterlocked.Update(ref _data, static (data, other) => data.Intersect(other), other);
    public void SymmetricExceptWith(IEnumerable<T> other)
        => ImmutableInterlocked.Update(ref _data, static (data, other) => data.SymmetricExcept(other), other);
    public void UnionWith(IEnumerable<T> other)
        => ImmutableInterlocked.Update(ref _data, static (data, other) => data.Union(other), other);
#pragma warning restore CS0420

    public void Clear() => _data = ImmutableHashSet<T>.Empty;

    public void CopyTo(T[] array, int arrayIndex)
    {
        foreach (var item in _data)
            array[arrayIndex++] = item;
    }

    private volatile ImmutableHashSet<T> _data = ImmutableHashSet<T>.Empty;
}
