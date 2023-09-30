using TairitsuSora.Core;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Ping : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "ping",
        Togglable = false,
        Listed = true,
        DisplayName = "Ping",
        Summary = "在线状态测试"
    };

    [MessageHandler(Description = "测试消息接收/发送是否正常")]
    public string MainCommand() => "Pong!";
}
