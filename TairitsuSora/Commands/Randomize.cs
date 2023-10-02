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

    [MessageHandler(Signature = "s $args", Description = "从 [args] 中随机选择一个选项")]
    public string Select(string[] args)
    {
        if (args.Length < 2) return "你想让我选什么？";
        if (args.All(s => s == args[0])) return "你是在耍我吗？";
        return $"命运选择了 {args.Sample()}";
    }

    [MessageHandler(Signature = "p $args", Description = "随机排列 [args] 里面各个部分")]
    public string Permute(string[] args)
    {
        if (args.Length < 2) return "你想让我排列什么？";
        if (args.All(s => s == args[0])) return "你是在耍我吗？";
        args.Shuffle();
        return $"排列结果: {string.Join(' ', args)}";
    }
}
