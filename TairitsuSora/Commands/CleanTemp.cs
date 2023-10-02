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
        await Task.Delay(TimeSpan.FromHours(1), Application.Instance.CancellationToken);
        try { Loop(); } catch { /* ignored */ }
    }

    private void Loop()
    {
        DirectoryInfo tempDir = new("./temp");
        if (!tempDir.Exists) return;
        foreach (var file in tempDir.EnumerateFiles())
        {
            if (DateTime.Now - file.LastWriteTime > TimeSpan.FromMinutes(30))
                file.Delete();
        }
    }
}
