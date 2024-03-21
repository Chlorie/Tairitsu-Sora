using System.Diagnostics;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Mahjong : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "mj",
        Summary = "麻将",
        Description = "搓一局吗？"
    };

    [MessageHandler(Signature = "hr $hand", Description = "类似天鳳牌理的手牌分析", ReplyException = true)]
    public async ValueTask<string> CommandHairi(string hand)
    {
        ProcessStartInfo procInfo = new()
        {
            WorkingDirectory = "tools",
            FileName = "tools/hairi",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList = { hand }
        };
        var proc = await procInfo.RunAsync(
            TimeSpan.FromMinutes(1), Application.Instance.CancellationToken);
        return await proc.StandardOutput.ReadToEndAsync();
    }
}
