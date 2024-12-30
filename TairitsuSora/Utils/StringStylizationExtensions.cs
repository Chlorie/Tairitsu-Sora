using System.Text;

namespace TairitsuSora.Utils;

public static class StringStylizationExtensions
{
    public static string ToSansBoldScript(this string text)
    {
        return text.Aggregate(new StringBuilder(), (current, c) => c switch
        {
            >= '0' and <= '9' => current.Append(char.ConvertFromUtf32(0x1d7ec + (c - '0'))),
            >= 'A' and <= 'Z' => current.Append(char.ConvertFromUtf32(0x1d5d4 + (c - 'A'))),
            >= 'a' and <= 'z' => current.Append(char.ConvertFromUtf32(0x1d5ee + (c - 'a'))),
            _ => current.Append(c)
        }).ToString();
    }

    public static string ToSansBoldItalicScript(this string text)
    {
        return text.Aggregate(new StringBuilder(), (current, c) => c switch
        {
            >= '0' and <= '9' => current.Append(char.ConvertFromUtf32(0x1d7ec + (c - '0'))),
            >= 'A' and <= 'Z' => current.Append(char.ConvertFromUtf32(0x1d63c + (c - 'A'))),
            >= 'a' and <= 'z' => current.Append(char.ConvertFromUtf32(0x1d656 + (c - 'a'))),
            _ => current.Append(c)
        }).ToString();
    }
}
