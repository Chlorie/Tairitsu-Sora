using System.Diagnostics;
using LanguageExt.UnitsOfMeasure;
using MeltySynth;
using Sora.Entities;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.Music;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Piano : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "p",
        Summary = "钢琴音乐播放",
        Description = "音乐输入语法请参考：https://hikari-music.readthedocs.io/en/latest/syntax.html"
    };

    [MessageHandler(Signature = "play $notation", Description = "生成钢琴音乐语音消息", ReplyException = true)]
    public async ValueTask CommandPlayAsync(GroupMessageEventArgs ev, FullString notation)
    {
        Guid guid = Guid.NewGuid();
        await File.WriteAllTextAsync($"temp/{guid}.hkr", notation.Text);
        await RunHikariToLilypond(guid);
        await RunLilypond(guid);

        string path = File.Exists($"temp/{guid}.midi") ? $"temp/{guid}.midi" : $"temp/{guid}.mid";
        MidiFile midi = new(path);
        if (midi.Length > 4.Minutes().ToTimeSpan())
        {
            await ev.QuoteReply("乐谱生成的对应音频过长。");
            return;
        }
        var record = new MessageBody().Record(await PianoSynth.FromMidi(midi));
        await ev.QuoteReply($"乐谱 ID: {guid}\n请注意该 ID 仅在短时间内有效，太久远的临时文件会被删除。");
        await ev.Reply(record);
    }

    [MessageHandler(Signature = "score $id", Description = "生成对应的五线谱", ReplyException = true)]
    public async ValueTask<MessageBody> CommandScoreAsync(string id)
    {
        Guid guid = Guid.Parse(id);
        string path = $"temp/{guid}.cropped.png";
        return new FileInfo(path).Exists
            ? new MessageBody().Image(await File.ReadAllBytesAsync(path))
            : $"并未能找到 ID 为 {guid} 的乐谱，请检查输入是否正确。注意，太久远的乐谱可能已经被删除。";
    }

    private async ValueTask RunHikariToLilypond(Guid guid)
    {
        string hkrFile = new FileInfo($"temp/{guid}.hkr").FullName;
        string lyFile = new FileInfo($"temp/{guid}.ly").FullName;
        ProcessStartInfo procInfo = new()
        {
            WorkingDirectory = "tools",
            FileName = "tools/hkr2ly",
            UseShellExecute = false,
            RedirectStandardError = true,
            ArgumentList = { hkrFile, lyFile }
        };
        var proc = await procInfo.RunAsync(1.Minutes(), Application.Instance.CancellationToken);
        var msg = await proc.StandardError.ReadToEndAsync();
        if (msg != "") throw new ArgumentException(msg);
    }

    private ValueTask<Process> RunLilypond(Guid guid) =>
        new ProcessStartInfo
        {
            FileName = "lilypond",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            ArgumentList =
            {
                "-fpng", "-dcrop", "-dno-print-pages",
                "-o", $"temp/{guid}", $"temp/{guid}.ly"
            }
        }.RunAsync(1.Minutes(), Application.Instance.CancellationToken);
}
