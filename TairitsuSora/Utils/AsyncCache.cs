namespace TairitsuSora.Utils;

public class AsyncCache<T>(Func<ValueTask<T>> factory, TimeSpan? invalidateAfter = null)
{
    public ValueTask<T> Get()
    {
        lock (factory)
            if (_lastUpdate is { } last && DateTime.Now - last < _invalidateAfter)
                return ValueTask.FromResult(_value!);
        return Update();
    }

    private T? _value;
    private DateTime? _lastUpdate;
    private readonly TimeSpan _invalidateAfter = invalidateAfter ?? TimeSpan.MaxValue;

    private async ValueTask<T> Update()
    {
        T value = await factory();
        lock (factory)
        {
            _value = value;
            _lastUpdate = DateTime.Now;
        }
        return _value;
    }
}
