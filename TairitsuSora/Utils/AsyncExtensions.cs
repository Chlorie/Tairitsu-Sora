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

    public static ValueTask IgnoreCancellation(this ValueTask task)
        => task.IgnoreException<OperationCanceledException>(false);

    public static async ValueTask AsValueTask(this Task task) => await task.ConfigureAwait(false);

    public static async ValueTask WhenAll(this IEnumerable<ValueTask> tasks)
    {
        var list = tasks.ToReadOnlyList();
        if (list.Count == 0) return;
        List<Exception>? exceptions = null;
        foreach (var task in list)
        {
            try { await task.ConfigureAwait(false); }
            catch (Exception ex) { (exceptions ??= new List<Exception>()).Add(ex); }
        }
        if (exceptions != null)
            throw new AggregateException(exceptions);
    }

    public static async ValueTask<TRes[]> WhenAll<TRes>(this IEnumerable<ValueTask<TRes>> tasks)
    {
        var list = tasks.ToReadOnlyList();
        if (list.Count == 0) return Array.Empty<TRes>();
        TRes[] result = new TRes[list.Count];
        List<Exception>? exceptions = null;
        for (int i = 0; i < list.Count; i++)
        {
            try { result[i] = await list[i].ConfigureAwait(false); }
            catch (Exception ex) { (exceptions ??= new List<Exception>()).Add(ex); }
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
}
