using System.Diagnostics;
using TairitsuSora.Core;
using YukariToolBox.LightLog;

namespace TairitsuSora.Utils;

public static class AsyncExtensions
{
    public static async ValueTask IgnoreException<T>(
        this ValueTask task, bool logException = true) where T : Exception
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (T e)
        {
            if (logException)
                Log.Error(e, Application.AppName, "Exception caught and ignored");
        }
    }

    public static ValueTask IgnoreException(this ValueTask task, bool logException = true)
        => task.IgnoreException<Exception>(logException);

    public static async ValueTask<TRes?> ExceptionAsNull<TRes, TExc>(
        this ValueTask<TRes> task, bool logException = true) where TRes : class where TExc : Exception
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (TExc e)
        {
            if (logException)
                Log.Error(e, Application.AppName, "Exception caught and ignored");
            return null;
        }
    }

    public static ValueTask<T?> ExceptionAsNull<T>(this ValueTask<T> task, bool logException = true) where T : class
        => ExceptionAsNull<T, Exception>(task, logException);

    public static async ValueTask<bool> ExceptionAsFalse(this ValueTask task, bool logException = true)
    {
        try
        {
            await task;
            return true;
        }
        catch (Exception e)
        {
            if (logException)
                Log.Error(e, Application.AppName, "Exception caught and ignored");
            return false;
        }
    }

    public static ValueTask IgnoreCancellation(this ValueTask task)
        => task.IgnoreException<OperationCanceledException>(false);

    public static ValueTask WaitUntilCanceled(this CancellationToken token)
        => Task.Delay(-1, token).AsValueTask().IgnoreCancellation();

    public static async ValueTask AsValueTask(this Task task) => await task.ConfigureAwait(false);

    public static async ValueTask WhenAll(this IEnumerable<ValueTask> tasks)
    {
        var list = tasks.ToReadOnlyList();
        if (list.Count == 0) return;
        List<Exception>? exceptions = null;
        foreach (var task in list)
        {
            try { await task.ConfigureAwait(false); }
            catch (Exception ex) { (exceptions ??= []).Add(ex); }
        }
        if (exceptions != null)
            throw new AggregateException(exceptions);
    }

    public static async ValueTask<TRes[]> WhenAll<TRes>(this IEnumerable<ValueTask<TRes>> tasks)
    {
        var list = tasks.ToReadOnlyList();
        if (list.Count == 0) return [];
        TRes[] result = new TRes[list.Count];
        List<Exception>? exceptions = null;
        for (int i = 0; i < list.Count; i++)
        {
            try { result[i] = await list[i].ConfigureAwait(false); }
            catch (Exception ex) { (exceptions ??= []).Add(ex); }
        }
        if (exceptions != null)
            throw new AggregateException(exceptions);
        return result;
    }

    public static ValueTask WhenAll(params ValueTask[] tasks) => tasks.WhenAll();
    public static ValueTask<TRes[]> WhenAll<TRes>(params ValueTask<TRes>[] tasks) => tasks.WhenAll();

    public static async ValueTask LoopUntilCancellation(Func<ValueTask> taskFactory)
    {
        try { while (true) await taskFactory(); }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Wait for a condition to be true.
    /// </summary>
    /// <param name="predicate">The condition.</param>
    /// <param name="timeout">The total time to wait.</param>
    /// <param name="interval">How much time to wait for each check.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Whether the condition turns true in time.</returns>
    public static async ValueTask<bool> RetryUntil(
        Func<ValueTask<bool>> predicate,
        TimeSpan timeout, TimeSpan interval,
        CancellationToken token = default)
    {
        DateTime end = DateTime.Now + timeout;
        while (DateTime.Now < end)
        {
            if (await predicate()) return true;
            await Task.Delay(interval, token);
        }
        return false;
    }

    public static async ValueTask<Process> RunAsync(
        this ProcessStartInfo procInfo, TimeSpan? timeout = null, CancellationToken token = default)
    {
        var proc = Process.Start(procInfo)!;
        using var src = CancellationTokenSource.CreateLinkedTokenSource(token);
        src.CancelAfter(timeout ?? Timeout.InfiniteTimeSpan);
        try
        {
            await proc.WaitForExitAsync(src.Token);
        }
        catch (OperationCanceledException)
        {
            proc.Kill();
            throw;
        }
        return proc;
    }
}
