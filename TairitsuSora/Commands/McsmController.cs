using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class McsmController : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "mcsm",
        Summary = "在群中控制 MCSManager"
    };

    public override ValueTask ApplyConfigAsync(JsonNode config)
    {
        var dict = config.Deserialize<Dictionary<long, McsmInstanceConfig>>() ?? [];
        _instances = new ConcurrentDictionary<long, McsmInstance>(dict.ToDictionary(
            kv => kv.Key, kv => kv.Value.CreateInstance()));
        return ValueTask.CompletedTask;
    }

    public override ValueTask<JsonNode?> CollectConfigAsync()
    {
        var configs = _instances.ToDictionary(kv => kv.Key, kv => kv.Value.Config);
        return ValueTask.FromResult(JsonSerializer.SerializeToNode(configs));
    }

    [MessageHandler(Signature = "ping", Description = "检查实例是否正常工作")]
    public async ValueTask<string> PingServer(GroupMessageEventArgs ev)
    {
        if (!_instances.TryGetValue(ev.SourceGroup.Id, out var inst))
            return "当前群没有绑定 MCSManager 实例";
        var data = await inst.PingInstance(_client);
        int statusCode = data["status"]!.GetValue<int>();
        if (statusCode != 3)
            return $"[{statusCode switch
            {
                0 => "已停止",
                1 => "正在停止",
                2 => "正在启动",
                _ => "未知"
            }}]";

        var processInfo = data["processInfo"]!;
        float cpuUsage = processInfo["cpu"]!.GetValue<float>();
        long memUsage = processInfo["memory"]!.GetValue<long>();
        var info = data["info"]!;
        string currentPlayers = info["currentPlayers"]!.GetValue<string>();
        string version = info["version"]!.GetValue<string>();
        return $"""
            [运行中]
            服务器版本: {version}
            当前在线人数: {currentPlayers}
            CPU 占用: {cpuUsage:0.00}%
            内存占用: {(float)memUsage / 1_000_000_000:0.00}GB
            """;
    }

    private record McsmInstanceConfig(
        long AdminId,
        string ApiEndpoint,
        string ApiKey,
        string Uuid,
        string RemoteUuid,
        bool EnableMessageForwarding = false
    )
    {
        public McsmInstance CreateInstance() => new() { Config = this };
    }

    private class McsmInstance
    {
        public McsmInstanceConfig Config { get; init; } = null!;

        public async Task<JsonNode> PingInstance(HttpClient client)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apikey"] = Config.ApiKey;
            query["uuid"] = Config.Uuid;
            query["remote_uuid"] = Config.RemoteUuid;
            var url = new UriBuilder($"{Config.ApiEndpoint}/api/instance") { Query = query.ToString() }.ToString();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!;
        }

        public async Task RunCommand(HttpClient client, string command)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apikey"] = Config.ApiKey;
            query["uuid"] = Config.Uuid;
            query["remote_uuid"] = Config.RemoteUuid;
            query["command"] = command;
            var url = new UriBuilder($"{Config.ApiEndpoint}/api/protected_instance/command")
            { Query = query.ToString() }.ToString();
            await client.GetAsync(url);
        }

        public async Task<string[]> UpdateLines(HttpClient client)
        {
            try { return await UpdateLinesImpl(client); }
            catch { return []; }
        }

        private string[] _cachedLines = [];

        private async Task<string[]> UpdateLinesImpl(HttpClient client)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["apikey"] = Config.ApiKey;
            query["uuid"] = Config.Uuid;
            query["remote_uuid"] = Config.RemoteUuid;
            query["size"] = "4096";
            var url = new UriBuilder($"{Config.ApiEndpoint}/api/protected_instance/outputlog")
            { Query = query.ToString() }.ToString();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            var lines = node["data"]!
                .GetValue<string>()
                .Split("\n")[1..]
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            if (_cachedLines.Length == 0)
            {
                _cachedLines = lines;
                return [];
            }
            if (lines.Length == 0) return lines;
            var cachedSet = _cachedLines.ToHashSet();
            _cachedLines = lines;
            lines = lines.Where(line => !cachedSet.Contains(line)).ToArray();
            return lines;
        }
    }

    private ConcurrentDictionary<long, McsmInstance> _instances = [];
    private HttpClient _client = new();
}
