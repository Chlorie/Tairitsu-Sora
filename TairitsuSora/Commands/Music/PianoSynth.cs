using System.Diagnostics;
using MeltySynth;
using System.Runtime.InteropServices;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.Music;

public readonly record struct NoteEvent(int SampleIndex, int Pitch, bool IsOn);

public class PianoSynth
{
    public const int SampleRate = 24000;

    public static async ValueTask<byte[]> FromNoteEvents(
        IEnumerable<NoteEvent> events, int sampleLength, CancellationToken token = default)
    {
        PianoSynth instance = Instance;
        using var lck = await instance._lock.LockAsync(token);
        return await EncodeToFile(instance.FromEventsImpl(events, sampleLength), token);
    }

    public static async ValueTask<byte[]> FromMidi(
        MidiFile midi, CancellationToken token = default)
    {
        PianoSynth instance = Instance;
        using var lck = await instance._lock.LockAsync(token);
        return await EncodeToFile(instance.FromMidiImpl(midi), token);
    }

    private static PianoSynth? _instance;
    private static PianoSynth Instance => _instance ??= new PianoSynth();
    private AsyncLock _lock = new();
    private Synthesizer? _synth;
    private Synthesizer Synth => _synth
        ??= new Synthesizer(SoundFontManager.LoadSoundFont("data/chateau-grand-lite-v1.0.sf2"), SampleRate);

    private byte[] FromEventsImpl(IEnumerable<NoteEvent> events, int sampleLength)
    {
        byte[] buffer = new byte[(sampleLength + SampleRate) * sizeof(short)];
        Span<short> samples = MemoryMarshal.Cast<byte, short>(buffer.AsSpan());
        int lastEventPos = 0;
        foreach (var note in events)
        {
            if (lastEventPos < note.SampleIndex)
            {
                Synth.RenderMonoInt16(samples[lastEventPos..note.SampleIndex]);
                lastEventPos = note.SampleIndex;
            }
            if (note.IsOn)
                Synth.NoteOn(0, note.Pitch, 85);
            else
                Synth.NoteOff(0, note.Pitch);
        }
        Synth.RenderMonoInt16(samples[lastEventPos..]);
        Synth.Reset();
        return buffer;
    }

    private byte[] FromMidiImpl(MidiFile midi)
    {
        MidiFileSequencer sequencer = new(Synth);
        sequencer.Play(midi, false);
        byte[] buffer = new byte[(int)((1 + midi.Length.TotalSeconds) * SampleRate) * sizeof(short)];
        Span<short> samples = MemoryMarshal.Cast<byte, short>(buffer.AsSpan());
        sequencer.RenderMonoInt16(samples);
        Synth.Reset();
        return buffer;
    }

    private static async ValueTask<byte[]> EncodeToFile(byte[] pcmBytes, CancellationToken token)
    {
        string inPath = $"./temp/{Guid.NewGuid()}.pcm";
        string outPath = inPath + ".wav";
        await File.WriteAllBytesAsync(inPath, pcmBytes, token);
        ProcessStartInfo procInfo = new()
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList =
            {
                "-f", "s16le",
                "-ar", SampleRate.ToString(),
                "-ac", "1",
                "-i", inPath, outPath
            }
        };
        await procInfo.RunAsync(token: token);
        return await File.ReadAllBytesAsync(outPath, token);
    }
}

public class SoundFontManager
{
    public static SoundFont LoadSoundFont(string path) => _instance.Value.LoadSoundFontImpl(path);

    private static Lazy<SoundFontManager> _instance = new(() => new SoundFontManager());

    private Dictionary<string, SoundFont> _soundFonts = [];

    public SoundFont LoadSoundFontImpl(string path)
    {
        string absolute = Path.GetFullPath(path);
        lock (_soundFonts)
        {
            if (_soundFonts.TryGetValue(absolute, out SoundFont? soundFont))
                return soundFont;
            SoundFont font = new(absolute);
            _soundFonts.Add(absolute, font);
            return font;
        }
    }
}
