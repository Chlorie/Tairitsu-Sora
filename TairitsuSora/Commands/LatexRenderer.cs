﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Sora.Entities;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class LatexRenderer : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "tex",
        Summary = "LaTeX 渲染",
        Description = "渲染带有 LaTeX 代码的消息"
    };

    public override async ValueTask ExecuteAsync()
    {
        Application.Service.Event.OnGroupMessage += OnGroupMessage;
        try { await Application.Instance.CancellationToken.WaitUntilCanceled(); }
        finally { Application.Service.Event.OnGroupMessage -= OnGroupMessage; }
    }

    private static readonly Regex TexCommand = new(@"\\\w+", RegexOptions.Compiled);

    private async ValueTask OnGroupMessage(string _, GroupMessageEventArgs eventArgs)
    {
        if (eventArgs.Message.MessageBody.GetIfOnlyText() is not { } text) return;
        if (!EnabledGroups.Contains(eventArgs.SourceGroup.Id)) return;
        if (!text.Contains('$') &&
            !text.Contains("\\(") && !text.Contains("\\)") &&
            !text.Contains("\\[") && !text.Contains("\\]") &&
            !TexCommand.IsMatch(text)) return;

        string template = await File.ReadAllTextAsync("data/template.tex");
        string content = template.Replace("%% user-text", text);
        Guid guid = Guid.NewGuid();
        await File.WriteAllTextAsync($"temp/{guid}.tex", content);

        await new ProcessStartInfo
        {
            FileName = "latexmk",
            UseShellExecute = false,
            WorkingDirectory = "temp",
            ArgumentList = { "-xelatex", "-interaction=nonstopmode", $"{guid}.tex" }
        }.RunAsync(TimeSpan.FromSeconds(40), Application.Instance.CancellationToken);
        if (!File.Exists($"temp/{guid}.pdf")) return;

        string convertExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "magick" : "convert";
        await new ProcessStartInfo
        {
            FileName = convertExe,
            UseShellExecute = false,
            ArgumentList =
            {
                "-density", "200", $"temp/{guid}.pdf",
                "-background", "white", "-alpha", "remove", "-alpha", "off",
                "-append", "-resize", "1280x",
                "-trim", "-bordercolor", "white", "-border", "15",
                "+repage", $"temp/{guid}.png"
            }
        }.RunAsync(TimeSpan.FromSeconds(20), Application.Instance.CancellationToken);
        if (!File.Exists($"temp/{guid}.png")) return;

        await eventArgs.Reply(new MessageBody().Image(await File.ReadAllBytesAsync($"temp/{guid}.png")));
    }
}
