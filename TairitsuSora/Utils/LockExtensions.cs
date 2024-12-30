namespace TairitsuSora.Utils;

public static class LockExtensions
{
    public static IDisposable WriteLock(this ReaderWriterLockSlim rwLock) => new WriteLockGuard(rwLock);

    public static IDisposable ReadLock(this ReaderWriterLockSlim rwLock) => new ReadLockGuard(rwLock);

    private class WriteLockGuard : IDisposable
    {
        public WriteLockGuard(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            _rwLock.EnterWriteLock();
        }

        public void Dispose() => _rwLock.ExitWriteLock();

        private readonly ReaderWriterLockSlim _rwLock;
    }

    private class ReadLockGuard : IDisposable
    {
        public ReadLockGuard(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            _rwLock.EnterReadLock();
        }

        public void Dispose() => _rwLock.ExitReadLock();

        private readonly ReaderWriterLockSlim _rwLock;
    }
}
