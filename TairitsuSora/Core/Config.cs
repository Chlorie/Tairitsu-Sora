using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TairitsuSora.Core;

public class BotConfig
{
    [JsonInclude] public long BotId;
    [JsonInclude] public OneBotConfig OneBotConfig = new();
    [JsonInclude] public long[] Admins = [];
    [JsonInclude] public Dictionary<string, HashSet<long>> CommandEnabledGroups = [];
    [JsonInclude] public Dictionary<string, JsonNode> CommandConfigs = [];

    public static BotConfig Load(string path)
    {
        string text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BotConfig>(text)!;
    }

    public void Save(string path)
    {
        // Backup the old config
        if (File.Exists(path))
        {
            string backup = path + ".backup";
            if (File.Exists(backup))
                File.Delete(backup);
            File.Move(path, backup);
        }
        File.WriteAllText(path, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}

public record OneBotConfig(string Host = "127.0.0.1", ushort Port = 8080, string AccessToken = "");
