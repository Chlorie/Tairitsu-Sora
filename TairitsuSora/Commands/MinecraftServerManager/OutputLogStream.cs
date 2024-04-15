namespace TairitsuSora.Commands.MinecraftServerManager;

public class OutputLogStream
{
    public (bool wasEmpty, string[] lines) UpdateLines(string[] newLines)
    {
        string[] diff = DiffLines(_cachedLines, newLines);
        bool wasEmpty = _cachedLines.Length == 0;
        if (diff.Length != 0) _cachedLines = newLines;
        return (wasEmpty, diff);
    }

    private string[] _cachedLines = [];

    private static string[] DiffLines(string[] oldLines, string[] newLines)
    {
        if (newLines.Length == 0) return [];
        if (oldLines.Length == 0) return newLines;
        int idx = Array.LastIndexOf(newLines, oldLines[^1]);
        return idx == -1 ? newLines : newLines[(idx + 1)..];
    }
}
