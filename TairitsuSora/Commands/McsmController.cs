using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageExt;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.MinecraftServerManager;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class McsmController : Command, IDisposable
{
    public override CommandInfo Info => new()
    {
        Trigger = "mcsm",
        Summary = "在群中控制 MCSManager",
        Listed = false
    };

    public override ValueTask ApplyConfigAsync(JsonNode config)
    {
        var dict = config.Deserialize<Dictionary<long, ControllerConfig>>() ?? [];
        _instances = new ConcurrentDictionary<long, ControllerInstance>(dict.ToDictionary(
            kv => kv.Key, kv => new ControllerInstance(kv.Value, kv.Key, _client)));
        return ValueTask.CompletedTask;
    }

    public override ValueTask<JsonNode?> CollectConfigAsync()
    {
        var configs = _instances.ToDictionary(kv => kv.Key, kv => kv.Value.Config);
        return ValueTask.FromResult(JsonSerializer.SerializeToNode(configs));
    }

    public override async ValueTask ExecuteAsync()
        => await Task.WhenAll(_instances.Values.Select(inst => inst.Run().AsTask()));

    [MessageHandler(Signature = "msgfwd $enabled", Description = "开启/关闭消息同步")]
    public string ToggleMessageForwarding(GroupMessageEventArgs ev, bool enabled)
        => CheckAdmin(ev).Match(
            Left: inst =>
            {
                inst.EnableMessageForwarding = enabled;
                return enabled ? "已开启消息同步" : "已关闭消息同步";
            },
            Right: msg => msg
        );

    [MessageHandler(Signature = "autobackup $enabled", Description = "开启/关闭每日自动备份")]
    public string ToggleDailyBackup(GroupMessageEventArgs ev, bool enabled)
        => CheckAdmin(ev).Match(
            Left: inst =>
            {
                inst.EnableDailyBackup = enabled;
                return enabled ? "已开启每日自动备份" : "已关闭每日自动备份";
            },
            Right: msg => msg
        );

    [MessageHandler(Signature = "ping", Description = "检查实例是否正常工作", ReplyException = true)]
    public async ValueTask<string> PingServer(GroupMessageEventArgs ev)
    {
        string DisplayRunningStatus(PingData data)
        {
            StringBuilder sb = new("[运行中]\n");
            sb.AppendLine($"服务器版本: {data.Info!.Version}");
            sb.AppendLine($"当前在线人数: {data.Info.OnlinePlayerCount}");
            if (data.ProcessInfo!.CpuUsage >= 0.01f)
                sb.AppendLine($"CPU 占用: {data.ProcessInfo.CpuUsage:0.00}%");
            sb.AppendLine($"内存占用: {(float)data.ProcessInfo.MemoryUsage / 1_000_000_000:0.00}GB");
            return sb.ToString();
        }

        return !_instances.TryGetValue(ev.SourceGroup.Id, out var inst)
            ? "当前群没有绑定 MCSManager 实例"
            : await inst.Ping() switch
            {
                { Status: PingStatusCode.Stopped } => "[已停止]",
                { Status: PingStatusCode.Stopping } => "[正在停止]",
                { Status: PingStatusCode.Starting } => "[正在启动]",
                { Status: PingStatusCode.Unknown } => "[未知]",
                var data => DisplayRunningStatus(data)
            };
    }

    [MessageHandler(Signature = "backup", Description = "备份当前世界数据", ReplyException = true)]
    public async ValueTask BackupWorld(GroupMessageEventArgs ev)
    {
        var maybeInst = CheckAdmin(ev);
        if (maybeInst.IsRight)
            await ev.QuoteReply(maybeInst.GetRight());
        else
            await maybeInst.GetLeft().MakeBackup();
    }

    [MessageHandler(Signature = "restart $forced", Description = "重启服务器，若 $forced 则强制重启", ReplyException = true)]
    public async ValueTask RestartServer(GroupMessageEventArgs ev, bool forced = false)
    {
        var maybeInst = CheckAdmin(ev);
        if (maybeInst.IsRight)
            await ev.QuoteReply(maybeInst.GetRight());
        else
            await maybeInst.GetLeft().RestartServer(true, forced);
    }

    [MessageHandler(Signature = "seed", Description = "获取当前世界的种子", ReplyException = true)]
    public async ValueTask<string> GetSeed(GroupMessageEventArgs ev)
    {
        if (!_instances.TryGetValue(ev.SourceGroup.Id, out var inst))
            return "当前群没有绑定 MCSManager 实例";
        string seed = await inst.GetSeed();
        return $"种子：{seed}";
    }

    public void Dispose()
    {
        foreach (var inst in _instances.Values)
            inst.Dispose();
        _client.Dispose();
    }

    private ConcurrentDictionary<long, ControllerInstance> _instances = [];
    private HttpClient _client = new();

    private Either<ControllerInstance, string> CheckAdmin(GroupMessageEventArgs ev)
    {
        if (!_instances.TryGetValue(ev.SourceGroup.Id, out var inst))
            return "当前群没有绑定 MCSManager 实例";
        if (ev.SenderInfo.UserId != inst.Config.AdminId)
            return "仅服务器管理员可以执行此操作";
        return inst;
    }
}
