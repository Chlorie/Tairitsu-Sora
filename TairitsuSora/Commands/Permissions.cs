using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Permissions : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "perm",
        Togglable = false,
        Summary = "指令权限管理"
    };

    [MessageHandler(Signature = "list", Description = "列出本群中启用的指令")]
    public string ListCommands(GroupMessageEventArgs ev) =>
        "本群中启用的指令: " + string.Join(", ", Application.Instance.Commands
            .Where(cmd => cmd.Info is { Listed: true, Trigger: not null }
                          && cmd.Command.IsEnabledInGroup(ev.SourceGroup.Id))
            .Select(cmd => cmd.Info.Trigger));

    [MessageHandler(Signature = "enable", Description = "启用所有指令")]
    public string EnableAllCommands(GroupMessageEventArgs ev) => ToggleAllCommands(ev, true);

    [MessageHandler(Signature = "disable", Description = "禁用所有指令")]
    public string DisableAllCommands(GroupMessageEventArgs ev) => ToggleAllCommands(ev, false);

    [MessageHandler(Signature = "enable $cmdName", Description = "启用指令 [cmdName]")]
    public string EnableCommand(GroupMessageEventArgs ev, string cmdName) => ToggleCommand(ev, cmdName, true);

    [MessageHandler(Signature = "disable $cmdName", Description = "禁用指令 [cmdName]")]
    public string DisableCommand(GroupMessageEventArgs ev, string cmdName) => ToggleCommand(ev, cmdName, false);

    private RegisteredCommand? FindCommand(string name)
        => Application.Instance.Commands.FirstOrDefault(cmd => cmd.Info.Trigger == name);

    private string ToggleAllCommands(GroupMessageEventArgs ev, bool enabled)
    {
        if (!ev.SenderInfo.IsAdmin())
            return "只有管理员可以使用此指令";
        foreach (var cmd in Application.Instance.Commands)
            if (cmd.Info.Togglable)
                cmd.Command.ToggleGroupAvailability(ev.SourceGroup.Id, enabled);
        return $"已在本群{(enabled ? "启用" : "禁用")}所有指令";
    }

    private string ToggleCommand(GroupMessageEventArgs ev, string cmdName, bool enabled)
    {
        if (!ev.SenderInfo.IsAdmin())
            return "只有管理员可以使用此指令";
        if (FindCommand(cmdName) is not { } cmd)
            return $"未找到 {cmdName} 指令";
        if (!cmd.Info.Togglable)
            return $"{cmdName} 指令强制常开，不可切换权限";
        cmd.Command.ToggleGroupAvailability(ev.SourceGroup.Id, enabled);
        return $"已在本群{(enabled ? "启用" : "禁用")} {cmdName} 指令";
    }
}
