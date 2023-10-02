using Sora.Entities;
using Sora.EventArgs.SoraEvent;
using System.Diagnostics;
using System.Text;
using TairitsuSora.Commands.Music;
using TairitsuSora.Core;
using TairitsuSora.Utils;
using YukariToolBox.LightLog;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class QuickChord : GroupGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "qc",
        Summary = "和弦听辨练习",
        Description = "看看你的耳朵够不够灵敏。我会随机生成一些和弦，你需要回答这个和弦的名字。答错无惩罚。你能答对多少题呢？"
    };

    [MessageHandler(Signature = "$time $flags", Description =
        "开始答题，在播放和弦之前会先播放一个 C 音作为基准。\n" +
        "  每道题时间限制为 [time = 10s~2min]；\n" +
        "  可使用 [flags] 开启三种不同变体：\n" +
        "    指明 i 时答案需考虑和弦转位（写出根音）；\n" +
        "    指明 a 时不播放基准音；\n" +
        "    指明 n 时用空格分隔的音名而非和弦名来作答。")]
    public ValueTask MainCommand(GroupMessageEventArgs ev, [ShowDefaultValueAs("30s")] TimeSpan? time = null, string? flags = null)
        => StartGame(ev, (e, _) => DoGameProcedureAsync(e, time ?? TimeSpan.FromSeconds(30), flags ?? ""));

    private async ValueTask DoGameProcedureAsync(GroupMessageEventArgs ev, TimeSpan time, string flags)
    {
        bool inversion = false, absolute = false, nonChord = false;
        foreach (char ch in flags)
            switch (ch)
            {
                case 'i':
                    inversion = true;
                    continue;
                case 'a':
                    absolute = true;
                    continue;
                case 'n':
                    nonChord = true;
                    continue;
                default:
                    if (!char.IsWhiteSpace(ch))
                    {
                        await ev.QuoteReply($"未知参数 {ch}");
                        return;
                    }
                    continue;
            }
        await RunQuizAsync(ev, time, inversion, absolute, nonChord);
    }

    private async Task RunQuizAsync(GroupMessageEventArgs ev, TimeSpan time,
        bool inversion, bool absolute, bool nonChord)
    {
        if (time.TotalSeconds > 120)
        {
            await ev.QuoteReply("两分钟连一首歌都听完了你怎么连个和弦都认不出来呢？？？");
            return;
        }
        bool tooShort = time.TotalSeconds < 10;
        if (tooShort) time = TimeSpan.FromSeconds(10);

        Func<Chord>[] levels =
        {
            Level1, Level2, Level3, Level4, Level5,
            Level6, Level7, Level8, Level9, Level10
        };
        await ev.QuoteReply("请准备好，测试将在 3 秒后开始...");
        await Task.Delay(3000);
        for (int i = 0; i < 30; i++)
        {
            Chord chord = levels[i < 20 && !tooShort ? i / 2 : 9]();
            List<int> noteIds = GenerateRandomChordVoicing(chord);
            await ev.Reply($"Q{i + 1}:");
            await ev.Reply(await GenerateQuizRecord(noteIds, absolute));
            if (await Application.EventChannel.WaitNextGroupMessage(
                    next => next.FromSameMember(ev) &&
                            CheckAnswer(chord, next.Message.MessageBody.GetIfOnlyText(), inversion, nonChord),
                    time)
                is not null) continue;
            MessageBody msg = new();
            msg.AddText($"最终分数: {i}\nA{i + 1}: {chord.Name}");
            if (await new ChordDrawer().DrawAsync(noteIds) is { } png) msg.Image(png);
            if (tooShort) msg.AddText("\n选个 10 秒以下你不是牛吗，怎么答不出来呢？");
            await ev.Reply(msg);
            return;
        }
        await ev.Reply("这什么神仙？？？");
    }

    private async ValueTask<MessageBody> GenerateQuizRecord(List<int> noteIds, bool noHint)
    {
        const int sampleRate = PianoSynth.SampleRate;
        List<NoteEvent> events = new(noteIds.Count * 2 + (noHint ? 0 : 2));
        if (!noHint)
        {
            events.Add(new NoteEvent(sampleRate / 2, 60, true));
            events.Add(new NoteEvent(sampleRate, 60, false));
        }
        int intro = noHint ? 0 : sampleRate;
        events.AddRange(noteIds.Select(note => new NoteEvent(sampleRate / 2 + intro, note, true)));
        events.AddRange(noteIds.Select(note => new NoteEvent(7 * sampleRate / 2 + intro, note, false)));
        return new MessageBody().Record(await PianoSynth.FromNoteEvents(events, 4 * sampleRate + intro));
    }

    private List<int> GenerateRandomChordVoicing(Chord chord)
    {
        static int ToOctaveStartingAt(NoteValue note, int lower)
        {
            int octaves = (lower + 11 - (int)note) / 12;
            return (int)note + 12 * octaves;
        }

        int noteCount = chord.Count;
        int lower = 60 - 4 * noteCount, higher = 60 + 4 * noteCount;

        NoteValue bass = chord.BassNote;
        List<int> result = new() { ToOctaveStartingAt(bass, lower) };
        lower = result[0];

        NoteValue[] notes = chord.ToArray();
        notes.Shuffle();
        int i = 0;
        foreach (NoteValue note in notes)
        {
            if (note == bass) continue;
            int currentLower = (higher - 12 - lower) * ++i / (noteCount - 1) + lower;
            if (currentLower < lower) currentLower = lower;
            result.Add(ToOctaveStartingAt(note, currentLower));
        }
        return result;
    }

    private bool CheckAnswer(Chord groundTruth, string? answer, bool inversion, bool nonChord)
    {
        if (answer is null) return false;
        try
        {
            Chord answerChord = nonChord
                ? new Chord(answer
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => NoteName.ParseFrom(s.Trim()).Value))
                : Chord.ParseFrom(answer);
            if (!inversion) answerChord.BassNote = groundTruth.BassNote;
            return answerChord == groundTruth;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Chord RandomInversion(Chord chord)
    {
        chord.BassNote = chord.ToArray().Sample();
        return chord;
    }

    public static Chord Level1()
    {
        Chord[] chords =
        {
            NoteValue.C.Maj(), NoteValue.F.Maj(), NoteValue.G.Maj(),
            NoteValue.D.Min(), NoteValue.E.Min(), NoteValue.A.Min()
        };
        return chords.Sample();
    }

    public static Chord Level2() => RandomInversion(Level1());

    public static Chord Level3()
    {
        NoteValue[] notes =
        {
            NoteValue.C, NoteValue.D, NoteValue.E,
            NoteValue.F, NoteValue.G, NoteValue.A
        };
        List<Chord> chords = new();
        foreach (NoteValue note in notes)
        {
            chords.Add(note.Maj());
            chords.Add(note.Min());
        }
        chords.Add(NoteValue.B.Min());
        chords.Add(NoteValue.BFlat.Maj());
        return RandomInversion(chords.Sample());
    }

    public static Chord Level4()
    {
        NoteValue note = (NoteValue)Random.Shared.Next(12);
        Chord chord = Random.Shared.Next(2) == 0 ? note.Maj() : note.Min();
        return RandomInversion(chord);
    }

    public static Chord Level5()
    {
        if (Random.Shared.Next(3) == 0) return Level4();
        Func<NoteValue, Chord>[] factories =
        {
            NoteValueExtensions.Aug,
            NoteValueExtensions.Dim,
            NoteValueExtensions.Sus
        };
        NoteValue note = (NoteValue)Random.Shared.Next(12);
        return RandomInversion(factories.Sample()(note));
    }

    public static Chord Level6()
    {
        if (Random.Shared.Next(3) == 0) return Level5();
        Func<NoteValue, Chord>[] factories =
        {
            NoteValueExtensions.Dom7,
            NoteValueExtensions.Dom7Sus,
            NoteValueExtensions.Maj7,
            NoteValueExtensions.Min7,
            NoteValueExtensions.MinMaj7,
            NoteValueExtensions.Min7Flat5,
            NoteValueExtensions.Dim7
        };
        NoteValue note = (NoteValue)Random.Shared.Next(12);
        return RandomInversion(factories.Sample()(note));
    }

    public static Chord Level7() => RandomTertianChord(false, false);

    public static Chord Level8() => RandomTertianChord(true, false);

    public static Chord Level9() => RandomTertianChord(true, true);

    public static Chord Level10()
    {
        ushort value = (ushort)Random.Shared.Next(1, 1 << 12);
        return RandomInversion(new Chord(value));
    }

    private static Chord RandomTertianChord(bool eleventh, bool thirteenth) =>
        Random.Shared.Next(2) == 0
            ? RandomNonAlteredTertianChord(eleventh, thirteenth)
            : RandomExtendedDom7Chord(eleventh, thirteenth);

    private static Chord RandomExtendedDom7Chord(bool eleventh, bool thirteenth)
    {
        static int RandomAlteration() =>
            Random.Shared.Next(4) == 0 ? 2 * Random.Shared.Next(2) - 1 : 0;

        NoteValue root = (NoteValue)Random.Shared.Next(12);
        Chord chord = root.Dom7();
        if (Random.Shared.Next(2) == 0) // Has ninth
            chord.Add(root.IntervalUp(2 + RandomAlteration()));
        if (eleventh && Random.Shared.Next(2) == 0) // Has eleventh
            chord.Add(root.IntervalUp(6)); // Must be A11 to avoid clashing with M3
        if (thirteenth && Random.Shared.Next(2) == 0) // Has thirteenth
            chord.Add(root.IntervalUp(9 + RandomAlteration()));
        return RandomInversion(chord);
    }

    private static Chord RandomNonAlteredTertianChord(bool eleventh, bool thirteenth)
    {
        Func<NoteValue, Chord>[] triads =
        {
            NoteValueExtensions.Maj,
            NoteValueExtensions.Min,
            NoteValueExtensions.Aug,
            NoteValueExtensions.Dim,
            NoteValueExtensions.Sus,
            NoteValueExtensions.Sus2
        };
        NoteValue root = (NoteValue)Random.Shared.Next(12);
        Chord chord = triads.Sample()(root);
        if (Random.Shared.Next(2) == 0) // Has seventh
            chord.Add(root.IntervalUp(10 + (Random.Shared.Next(3) == 0 ? 1 : 0)));
        if (Random.Shared.Next(2) == 0) // Has ninth
            chord.Add(root.IntervalUp(2));
        if (eleventh && Random.Shared.Next(2) == 0) // Has eleventh
            chord.Add(root.IntervalUp(6)); // Must be A11 to avoid clashing with M3
        if (thirteenth && Random.Shared.Next(2) == 0) // Has thirteenth
            chord.Add(root.IntervalUp(9));
        return RandomInversion(chord);
    }

    private class ChordDrawer
    {
        private List<int> _notes = null!;

        public async Task<byte[]?> DrawAsync(List<int> notes)
        {
            try
            {
                return await DrawAsyncImpl(notes);
            }
            catch (Exception e)
            {
                Log.Error(e, Application.AppName, "Failed to draw chord");
                return null;
            }
        }

        private async Task<byte[]?> DrawAsyncImpl(List<int> notes)
        {
            _notes = notes;
            string tempName = Guid.NewGuid().ToString();
            if (StartLilypond(tempName) is not { } proc) return null;
            WriteFile(proc.StandardInput);
            await proc.WaitForExitAsync();
            return await File.ReadAllBytesAsync(ResultFilePath(tempName));
        }

        private IEnumerable<(NoteName name, int octaveMod)> GetNoteNames()
        {
            Chord chord = new();
            foreach (int note in _notes) chord.Add((NoteValue)(note % 12));
            chord.BassNote = (NoteValue)(_notes[0] % 12);
            var noteNames = chord.FindNames().noteNames;
            var values = noteNames.Select(n => n.Value).ToArray();
            foreach (int note in _notes)
            {
                int idx = values.IndexOf((NoteValue)(note % 12));
                chord.FindNames();
                NoteName name = noteNames[idx];
                int octaveMod = note / 12 - 4;
                yield return (name, octaveMod);
            }
        }

        private Process? StartLilypond(string tempName)
        {
            ProcessStartInfo procInfo = new()
            {
                FileName = "lilypond",
                UseShellExecute = false,
                RedirectStandardInput = true,
                ArgumentList =
                {
                    "-fpng", "-dcrop", "-dno-print-pages",
                    "-o", $"temp/{tempName}", "-"
                }
            };
            return Process.Start(procInfo);
        }

        private string ResultFilePath(string tempName) => $"./temp/{tempName}.cropped.png";

        private (string, string) GenerateNoteSpecs()
        {
            static void AppendAccidental(StringBuilder sb, int accidental)
            {
                if (accidental > 0)
                    for (int i = 0; i < accidental; i++)
                        sb.Append("is");
                else
                    for (int i = 0; i < -accidental; i++)
                        sb.Append("es");
            }

            StringBuilder treble = new(), bass = new();

            void AddNote(NoteName name, int octaveMod)
            {
                if (octaveMod > 0)
                {
                    treble.Append(' ');
                    treble.Append(name.White.ToString().ToLower());
                    AppendAccidental(treble, name.Accidental);
                    treble.Append('\'', octaveMod);
                }
                else
                {
                    bass.Append(' ');
                    bass.Append(name.White.ToString().ToLower());
                    AppendAccidental(bass, name.Accidental);
                    bass.Append(',', -octaveMod);
                }
            }

            foreach ((NoteName name, int octaveMod) in GetNoteNames()) AddNote(name, octaveMod);
            return (treble.ToString(), bass.ToString());
        }

        private void WriteFile(StreamWriter stream)
        {
            new DirectoryInfo("./temp").Create();
            (string treble, string bass) = GenerateNoteSpecs();
            string str = $@"
\version ""2.22.1"" {{
    \new PianoStaff
    <<
        \new Staff {{
            \clef treble
            \omit Staff.TimeSignature
            <{treble} >1
        }}
        \new Staff {{
            \clef bass
            \omit Staff.TimeSignature
            <{bass} >1
        }}
    >>
}}
";
            stream.Write(str);
            stream.Write('\x1a');
            stream.Close();
        }
    }
}
