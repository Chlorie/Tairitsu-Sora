using System.Reflection;
using System.Text;
using Sora.Entities;
using Sora.Entities.Segment.DataModel;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Utils;
using YukariToolBox.LightLog;

namespace TairitsuSora.Core;

public class RegisteredCommand
{
    public Command Command { get; }
    public string Name { get; }
    public CommandInfo Info { get; }
    public string HelpMessage => _helpMsg ??= GenerateHelpMessage();

    public RegisteredCommand(Command cmd)
    {
        Command = cmd;
        Name = cmd.GetType().FullName!;
        Info = cmd.Info;
        _cmdMethods = RegisterCommandMethods(cmd)
            .OrderBy(m => m.SignatureDescription).ToArray();
    }

    public async ValueTask InitializeAsync()
    {
        await Command.InitializeAsync();
        Log.Info(Application.AppName, $"Initialized command {Name}");
    }

    public async ValueTask ExecuteAsync()
    {
        ValueTask Callback(string type, GroupMessageEventArgs eventArgs)
            => OnGroupMessage(eventArgs).IgnoreException();

        Application.Service.Event.OnGroupMessage += Callback;
        try { await Command.ExecuteAsync(); }
        finally { Application.Service.Event.OnGroupMessage -= Callback; }
    }

    private CommandMethod[] _cmdMethods;
    private string? _trigger;
    private string? _helpMsg;

    private string Trigger => _trigger ??= $"{Command.TriggerPrefix}{Info.Trigger}";

    private static List<CommandMethod> RegisterCommandMethods(Command cmd)
    {
        List<CommandMethod> res = new();
        foreach (var method in cmd.GetType().GetMethods())
            if (method.GetCustomAttribute<MessageHandlerAttribute>() is { } attr)
                res.Add(CommandMethod.Create(method, attr));
        return res;
    }

    private async ValueTask OnGroupMessage(GroupMessageEventArgs eventArgs)
    {
        if (_cmdMethods.Length == 0 || Info.Trigger is null) return;
        MessageBody msg = eventArgs.Message.MessageBody;
        var (trigger, remaining) = msg.SeparateFirstToken();
        if (trigger.Data is not TextSegment { Content: var text } || text != Trigger) return;
        if (Command.Info.Togglable && !Command.IsEnabledInGroup(eventArgs.SourceGroup.Id)) return;

        int maxMatch = -1;
        var matchFailures = new CommandMatchFailure?[_cmdMethods.Length];
        for (int i = 0; i < _cmdMethods.Length; i++)
        {
            var match = _cmdMethods[i].TryMatch(remaining);
            if (match.HoldsResult)
            {
                var args = match.Result;
                await _cmdMethods[i].Invoke(Command, args, eventArgs);
                return;
            }
            matchFailures[i] = match.Error;
            maxMatch = Math.Max(maxMatch, match.Error.MatchedParameterCount);
        }
        StringBuilder sb = new("未能匹配该指令");
        for (int i = 0; i < _cmdMethods.Length; i++)
        {
            if (matchFailures[i] is not { } failure || failure.MatchedParameterCount < maxMatch) continue;
            sb.Append($"\n{Command.TriggerPrefix}{Info.Trigger}");
            string signature = _cmdMethods[i].SignatureDescription;
            if (signature.Length > 0) sb.Append(' ').Append(signature);
            sb.Append($":\n    {failure.Message}");
        }
        await eventArgs.QuoteReply(sb.ToString());
    }

    private string GenerateHelpMessage()
    {
        StringBuilder sb = new($"{Info.DisplayName ?? Command.GetType().Name}");
        foreach (var method in _cmdMethods)
        {
            sb.Append('\n').Append(Command.TriggerPrefix).Append(Info.Trigger);
            string sig = method.SignatureDescription;
            if (sig != "")
                sb.Append(' ').Append(sig);
            if (method.HandlerAttribute.Description is { } desc)
                sb.Append(":\n    ").Append(desc);
        }
        if (Command.Info.Description is { } cmdDesc)
            sb.Append('\n').Append(cmdDesc);
        return sb.ToString();
    }
}
