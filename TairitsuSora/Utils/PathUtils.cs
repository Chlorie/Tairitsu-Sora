namespace TairitsuSora.Utils;

public static class PathUtils
{
    public static string? Parent(string path)
        => path == "/" ? null : Path.GetDirectoryName(path.TrimEnd('/'))?.Replace('\\', '/');

    public static string Combine(string path1, string path2)
        => Path.Combine(path1, path2).Replace('\\', '/');
}
