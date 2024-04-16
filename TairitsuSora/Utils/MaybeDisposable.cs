namespace TairitsuSora.Utils;

public class MaybeDisposable(object obj) : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (obj is IDisposable d)
            d.Dispose();
    }
}
