using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Randomize : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "r",
        Summary = "随机各种东西",
        Description = "一切都是命运的选择"
    };

    public override ValueTask ApplyConfigAsync(JsonNode config)
    {
        _personalLists = config.Deserialize<
            ConcurrentDictionary<long, ConcurrentDictionary<string, string[]>>>() ?? [];
        return ValueTask.CompletedTask;
    }

    public override ValueTask<JsonNode?> CollectConfigAsync()
        => ValueTask.FromResult(JsonSerializer.SerializeToNode(_personalLists));

    [MessageHandler(Signature = "s $args", Description = "从 [args] 中随机选择一个选项，若 [args] 仅有一项则从对应名字的自定义列表中选择")]
    public string Select(GroupMessageEventArgs ev, string[] args)
    {
        if (args.Length == 1)
        {
            if (GetPredefinedList(ev.SenderInfo.UserId, args[0]) is not { } list)
                return $"未找到选项列表 {args[0]}";
            args = list;
        }
        if (args.Length < 2) return "你想让我选什么？";
        if (args.All(s => s == args[0])) return "你是在耍我吗？";
        return $"命运选择了 {args.Sample()}";
    }

    [MessageHandler(Signature = "p $args", Description = "随机排列 [args] 里面各个部分，若 [args] 仅有一项则排列对应名字的自定义列表")]
    public string Permute(GroupMessageEventArgs ev, string[] args)
    {
        if (args.Length == 1)
        {
            if (GetPredefinedList(ev.SenderInfo.UserId, args[0]) is not { } list)
                return $"未找到选项列表 {args[0]}";
            args = list;
        }
        if (args.Length < 2) return "你想让我排列什么？";
        if (args.All(s => s == args[0])) return "你是在耍我吗？";
        args.Shuffle();
        return $"排列结果: {string.Join(' ', args)}";
    }

    [MessageHandler(Signature = "ls ls $page", Description = "列出自定义选择列表，每页 10 条")]
    public string ListPersonalLists(GroupMessageEventArgs ev, int page = 1)
    {
        if (!_personalLists.TryGetValue(ev.SenderInfo.UserId, out var dict) || dict.Count == 0) return "无自定义列表";
        int totalPages = (dict.Count - 1) / 10 + 1;
        page = Math.Clamp(page, 0, totalPages - 1);
        return $"[第 {page + 1} 页]\n" + string.Join('\n', dict.Skip(page * 10).Take(10).Select(kv
            => $"{kv.Key}：{string.Join(' ', kv.Value)}"));
    }

    [MessageHandler(Signature = "ls set $name $args", Description = "将名为 [name] 的自定义列表设为 [args]，若 [args] 为空则删除对应条目")]
    public string SetPersonalList(GroupMessageEventArgs ev, string name, string[] args)
    {
        if (args.Length == 1) return "选项数量需大于 2";
        var dict = _personalLists.GetOrAdd(ev.SenderInfo.UserId, []);
        if (args.Length == 0) return dict.Remove(name, out _) ? $"已删除列表 {name}" : $"未找到名为 {name} 的列表";
        bool updated = false;
        dict.AddOrUpdate(name, args, (_, _) =>
        {
            updated = true;
            return args;
        });
        return $"{(updated ? "已更新" : "已设置")}列表 {name}";
    }

    private ConcurrentDictionary<long, ConcurrentDictionary<string, string[]>> _personalLists = [];

    private string[]? GetPredefinedList(long userId, string name)
    {
        if (!_personalLists.TryGetValue(userId, out var dict)) return null;
        dict.TryGetValue(name, out var list);
        return list;
    }
}
