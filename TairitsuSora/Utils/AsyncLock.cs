namespace TairitsuSora.Utils;

public class AsyncLock
{
    public ValueTask<Releaser> LockAsync() => LockAsync(CancellationToken.None);

    public async ValueTask<Releaser> LockAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return new Releaser(_semaphore);
    }

    public struct Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            semaphore.Release();
        }

        private bool _disposed = false;
    }

    private SemaphoreSlim _semaphore = new(1);
}
