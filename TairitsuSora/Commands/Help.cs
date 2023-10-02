using TairitsuSora.Core;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Help : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "help",
        Togglable = false,
        Summary = "指令帮助"
    };

    [MessageHandler(Description = "获取指令列表")]
    public string ListCommands() => _helpMsg ??= GenerateHelpMessage();

    [MessageHandler(Signature = "$cmdName", Description = "获取对应指令详细说明")]
    public string ShowCommandHelp(string cmdName)
    {
        var cmd = Application.Instance.Commands.FirstOrDefault(cmd => cmd.Info.Trigger == cmdName);
        return cmd is null ? $"未找到名为 {cmdName} 的指令" : cmd.HelpMessage;
    }

    private string? _helpMsg;

    private string GenerateHelpMessage() =>
        $"使用 {TriggerPrefix}{Info.Trigger} [cmdName] 查看对应指令的说明" +
        string.Concat(Application.Instance.Commands
            .Where(cmd => cmd.Info is { Listed: true, Trigger: not null })
            .Select(cmd => $"\n{TriggerPrefix}{cmd.Info.Trigger}: {cmd.Info.Summary}"));
}
