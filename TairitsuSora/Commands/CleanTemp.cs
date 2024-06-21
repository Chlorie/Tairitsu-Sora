using LanguageExt.UnitsOfMeasure;
using TairitsuSora.Core;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class CleanTemp : Command
{
    public override CommandInfo Info => new()
    {
        Togglable = false,
        Listed = false
    };

    public override async ValueTask ExecuteAsync()
    {
        while (true)
        {
            try { Loop(); } catch { /* ignored */ }
            await Task.Delay(1.Hours(), Application.Instance.CancellationToken);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private void Loop()
    {
        DirectoryInfo tempDir = new("./temp");
        if (!tempDir.Exists) return;
        foreach (var file in tempDir.EnumerateFiles())
        {
            if (DateTime.Now - file.LastWriteTime > 30.Minutes().ToTimeSpan())
                file.Delete();
        }
    }
}
