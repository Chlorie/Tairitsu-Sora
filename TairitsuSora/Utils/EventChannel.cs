using System.Collections.Concurrent;
using Sora.EventArgs.SoraEvent;
using Sora.Interfaces;

namespace TairitsuSora.Utils;

public class EventChannel : IDisposable
{
    public EventChannel(ISoraService service)
    {
        _service = service;
        SetupEvents();
    }

    public void Dispose() => ResetEvents();

    public async ValueTask<GroupMessageEventArgs?> WaitNextGroupMessage(
        Predicate<GroupMessageEventArgs> predicate, TimeSpan timeout)
    {
        Waiter waiter = new(predicate);
        _waiters.TryAdd(waiter.Id, waiter);
        var res = await waiter.WaitNextEvent(timeout);
        _waiters.Remove(waiter.Id, out _);
        return res;
    }

    public async ValueTask<GroupMessageEventArgs?> WaitNextGroupMessage(
        Predicate<GroupMessageEventArgs> predicate, CancellationToken token)
    {
        Waiter waiter = new(predicate);
        _waiters.TryAdd(waiter.Id, waiter);
        var res = await waiter.WaitNextEvent(token);
        _waiters.Remove(waiter.Id, out _);
        return res;
    }

    private class Waiter(Predicate<GroupMessageEventArgs> predicate)
    {
        public Guid Id { get; } = Guid.NewGuid();

        public async ValueTask<GroupMessageEventArgs?> WaitNextEvent(TimeSpan timeout)
        {
            try { return await _semaphore.WaitAsync(timeout) ? _eventArgs : null; }
            catch (ObjectDisposedException) { return null; }
        }

        public async ValueTask<GroupMessageEventArgs?> WaitNextEvent(CancellationToken token)
        {
            try
            {
                await _semaphore.WaitAsync(token);
                return _eventArgs;
            }
            catch (Exception) { return null; }
        }

        public bool TryMatchEvent(GroupMessageEventArgs eventArgs)
        {
            if (!predicate(eventArgs)) return false;
            _eventArgs = eventArgs;
            _semaphore.Release();
            return true;
        }

        public void SignalSemaphore() => _semaphore.Release();

        private SemaphoreSlim _semaphore = new(0, 1);
        private GroupMessageEventArgs? _eventArgs;
    }

    private ISoraService _service;
    private ConcurrentDictionary<Guid, Waiter> _waiters = new();

    private void SetupEvents() => _service.Event.OnGroupMessage += OnGroupMessage;

    private void ResetEvents() => _service.Event.OnGroupMessage -= OnGroupMessage;

    private ValueTask OnGroupMessage(string _, GroupMessageEventArgs eventArgs)
    {
        foreach (var waiter in _waiters.Values)
            if (waiter.TryMatchEvent(eventArgs))
                break;
        return ValueTask.CompletedTask;
    }
}
