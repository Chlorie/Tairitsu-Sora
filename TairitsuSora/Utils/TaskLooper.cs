namespace TairitsuSora.Utils;

public class TaskLooper : IDisposable
{
    public TaskLooper(Func<ValueTask> taskFactory, CancellationToken token = default) : this(_ => taskFactory(), token) { }

    public TaskLooper(Func<CancellationToken, ValueTask> taskFactory, CancellationToken token = default)
    {
        _taskFactory = taskFactory;
        _stopSrc = CancellationTokenSource.CreateLinkedTokenSource(token);
        _stopToken = _stopSrc.Token;
        _stopToken.Register(CancelAll);
        _waitToken = _waitSrc.Token;
        _taskToken = _taskSrc.Token;
    }

    public bool Enabled
    {
        get
        {
            lock (_sync)
                return _running;
        }
        set
        {
            if (value) SetRunning();
            else SetWaiting();
        }
    }

    public async ValueTask Run()
    {
        while (!_stopToken.IsCancellationRequested)
        {
            await _waitToken.WaitUntilCanceled();
            lock (_sync)
            {
                _waitToken = ResetCancellationSource(ref _waitSrc);
                if (!_running) continue;
            }
            while (true)
            {
                await _taskFactory(_taskToken).IgnoreCancellation();
                lock (_sync)
                {
                    _taskToken = ResetCancellationSource(ref _taskSrc);
                    if (!_running) break;
                }
            }
        }
    }

    public void Stop() => _stopSrc.Cancel();

    public void Dispose()
    {
        Stop();
        _waitSrc.Dispose();
        _taskSrc.Dispose();
    }

    private object _sync = new();
    private bool _running;
    private CancellationTokenSource _stopSrc;
    private CancellationTokenSource _waitSrc = new();
    private CancellationTokenSource _taskSrc = new();
    private CancellationToken _stopToken;
    private CancellationToken _waitToken;
    private CancellationToken _taskToken;
    private Func<CancellationToken, ValueTask> _taskFactory;

    private void SetRunning()
    {
        lock (_sync)
        {
            if (_running) return;
            _running = true;
            _waitSrc.Cancel();
        }
    }

    private void SetWaiting()
    {
        lock (_sync)
        {
            if (!_running) return;
            _running = false;
            _taskSrc.Cancel();
        }
    }

    private void CancelAll()
    {
        lock (_sync)
        {
            _running = false;
            _waitSrc.Cancel();
            _taskSrc.Cancel();
        }
    }

    private CancellationToken ResetCancellationSource(ref CancellationTokenSource src)
    {
        if (!src.IsCancellationRequested) return src.Token;
        src.Dispose();
        src = new CancellationTokenSource();
        return src.Token;
    }
}
