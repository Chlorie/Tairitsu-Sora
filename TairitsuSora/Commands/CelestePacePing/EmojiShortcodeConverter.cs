using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TairitsuSora.Commands.CelestePacePing;

public class EmojiShortcodeConverter
{
    public static EmojiShortcodeConverter LoadFromMap(Dictionary<string, JsonElement> map)
    {
        EmojiShortcodeConverter converter = new();
        foreach (var (key, value) in map)
        {
            var emoji = string.Concat(key.Split('-')
                .Select(hex => char.ConvertFromUtf32(int.Parse(hex, NumberStyles.HexNumber))));
            if (value.ValueKind != JsonValueKind.String)
                foreach (var str in value.EnumerateArray())
                    converter._map[str.GetString()!] = emoji;
            else
                converter._map[value.GetString()!] = emoji;
        }
        return converter;
    }

    public string Convert(string input)
    {
        var parts = input.Split(':');
        if (parts.Length <= 2) return input;
        StringBuilder sb = new(parts[0]);
        int i = 1;
        for (; i < parts.Length - 1; i++)
        {
            if (_map.TryGetValue(parts[i], out var emoji))
                sb.Append(emoji).Append(parts[++i]);
            else
                sb.Append(':').Append(parts[i]);
        }
        if (i == parts.Length - 1) sb.Append(parts[^1]);
        return sb.ToString();
    }

    private Dictionary<string, string> _map = new();
}
