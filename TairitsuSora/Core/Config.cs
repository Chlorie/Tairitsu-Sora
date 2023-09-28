using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TairitsuSora.Core;

public class BotConfig
{
    [JsonInclude] public long BotId;
    [JsonInclude] public OneBotConfig OneBotConfig = new();
    [JsonInclude] public long[] Admins = Array.Empty<long>();
    [JsonInclude] public Dictionary<string, IReadOnlySet<long>> CommandEnabledGroups = new();
    [JsonInclude] public Dictionary<string, JsonNode> CommandConfigs = new();

    public static BotConfig Load(string path)
    {
        string text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BotConfig>(text)!;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this,
        new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
}

public record OneBotConfig(string Host = "127.0.0.1", ushort Port = 8080, string AccessToken = "");
