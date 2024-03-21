using System.Collections;
using System.Numerics;
using System.Text;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.Music;

public enum NoteValue
{
    CFlat = 11, C = 0, CSharp,
    DFlat = 1, D, DSharp,
    EFlat = 3, E, ESharp,
    FFlat = 4, F, FSharp,
    GFlat = 6, G, GSharp,
    AFlat = 8, A, ASharp,
    BFlat = 10, B, BSharp = 0
}

public enum NoteBase { C, D, E, F, G, A, B }

public readonly record struct NoteName(NoteBase White, int Accidental)
{
    public NoteValue Value => NoteValue.C.IntervalUp(WhiteKeyValue(White) + Accidental);

    public override string ToString() => @$"{White}{Accidental switch
    {
        -2 => "bb",
        -1 => "b",
        0 => "",
        1 => "#",
        2 => "x",
        var value => $"({value:+0;-#})"
    }}";

    public static NoteName ParseFrom(ref ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
            throw new ArgumentException("Empty note name");
        if (span[0] is < 'A' or > 'G')
            throw new ArgumentException($"Invalid note name {span[0]}");
        (int accidental, int parsed) = span.ElementAtOrDefault(1) switch
        {
            '#' => (1, 2),
            'x' => (2, 2),
            'b' => span.ElementAtOrDefault(2) == 'b' ? (-2, 3) : (-1, 2),
            _ => (0, 1)
        };
        NoteName result = new((NoteBase)((span[0] - 'C' + 7) % 7), accidental);
        span = span[parsed..];
        return result;
    }

    public static NoteName ParseFrom(ReadOnlySpan<char> span)
    {
        var result = ParseFrom(ref span);
        if (span.Length != 0)
            throw new ArgumentException("Extraneous characters after note name");
        return result;
    }

    public NoteName IntervalUp(int halfSteps, int degrees)
    {
        NoteBase white = (NoteBase)(((int)White + degrees - 1) % 7);
        NoteValue expectedValue = Value.IntervalUp(halfSteps);
        int accidental = (int)expectedValue - WhiteKeyValue(white);
        accidental = (accidental + 18) % 12 - 6;
        return new NoteName(white, accidental);
    }

    private static int WhiteKeyValue(NoteBase key) => key switch
    {
        NoteBase.C => 0,
        NoteBase.D => 2,
        NoteBase.E => 4,
        NoteBase.F => 5,
        NoteBase.G => 7,
        NoteBase.A => 9,
        NoteBase.B => 11,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
    };
}

public struct Chord : IEquatable<Chord>, IEnumerable<NoteValue>
{
    // 0~11 bits for whether that note value appears in this chord
    // 12~15 bits to save which note is the bass note

    public Chord() => Value = 0;
    public Chord(NoteValue bass) => Value = (ushort)((ushort)bass << 12);
    public Chord(ushort value) => Value = value;

    public Chord(IEnumerable<NoteValue> notes)
    {
        Value = 0;
        using var e = notes.GetEnumerator();
        if (!e.MoveNext()) return;
        var first = e.Current;
        Add(first);
        BassNote = first;
        while (e.MoveNext()) Add(e.Current);
    }

    public int Count => BitOperations.PopCount(Value & 0x0fffu);
    public ushort Value { get; private set; }

    public void Add(NoteValue note) => Value |= (ushort)(1 << (ushort)note);
    public void Remove(NoteValue note) => Value &= (ushort)~(1 << (ushort)note);

    public bool this[NoteValue note]
    {
        get => (Value & 1 << (ushort)note) != 0;
        set
        {
            if (value) Add(note);
            else Remove(note);
        }
    }

    public NoteValue BassNote
    {
        get => (NoteValue)(Value >> 12);
        set
        {
            Value &= 0x0fff;
            Value |= (ushort)((ushort)value << 12);
        }
    }

    public static bool operator ==(Chord left, Chord right) => left.Value == right.Value;
    public static bool operator !=(Chord left, Chord right) => left.Value != right.Value;
    public bool Equals(Chord other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Chord other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public IEnumerator<NoteValue> GetEnumerator()
    {
        for (int i = 0; i < 12; i++)
        {
            NoteValue note = (NoteValue)i;
            if (this[note])
                yield return note;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static Chord ParseFrom(ReadOnlySpan<char> str) => new ChordParser().Parse(str);
    public string Name => FindNames().chordName;
    public (string chordName, List<NoteName> noteNames) FindNames() => new ChordNamer().FindNames(this);

    private class ChordParser
    {
        private static readonly NoteValue[] CMajorNoteValues =
            { NoteValue.A, NoteValue.B, NoteValue.C, NoteValue.D, NoteValue.E, NoteValue.F, NoteValue.G };
        private static readonly int[] IntervalInHalfSteps =
            { 0, 0, 2, 4, 5, 7, 9, 10, 12, 14, 16, 17, 19, 21, 22, 24 };

        private enum ChordQuality { Default, Major, Minor, Diminished, Augmented, PowerChord }

        private Chord _chord;
        private ChordQuality _triad = ChordQuality.Default;
        private ChordQuality _seventh = ChordQuality.Default;
        private int _upmostInterval = 5;

        public Chord Parse(ReadOnlySpan<char> span)
        {
            if (span.Length == 0)
                throw new ArgumentException("Empty chord");
            _chord = [];
            ParseRoot(ref span);
            ParseChordQuality(ref span);
            ParseUpmostInterval(ref span);
            ParseAlteration(ref span);
            ParseSuspension(ref span);
            ParseAdditionOmission(ref span);
            ParseParensAddition(ref span);
            ParseSlash(ref span);
            if (!span.IsEmpty)
                throw new ArgumentException("Extraneous characters after chord notation");
            return _chord;
        }

        private void ParseRoot(ref ReadOnlySpan<char> span) => _chord.BassNote = ParseNote(ref span);

        private void ParseChordQuality(ref ReadOnlySpan<char> span)
        {
            void CheckMajorSeventh(ref ReadOnlySpan<char> span)
            {
                if (span.ConsumeIfStartsWith("maj"))
                    _seventh = ChordQuality.Major;
                else if (span.ConsumeIfStartsWith("ma"))
                    _seventh = ChordQuality.Major;
                else if (span.ConsumeIfStartsWith("M"))
                    _seventh = ChordQuality.Major;
            }

            if (span.ConsumeIfStartsWith("maj"))
            {
                _triad = ChordQuality.Major;
                _seventh = ChordQuality.Major;
            }
            else if (span.ConsumeIfStartsWith("min"))
            {
                _triad = ChordQuality.Minor;
                CheckMajorSeventh(ref span);
            }
            else if (span.ConsumeIfStartsWith("ma"))
            {
                _triad = ChordQuality.Major;
                _seventh = ChordQuality.Major;
            }
            else if (span.ConsumeIfStartsWith("mi"))
            {
                _triad = ChordQuality.Minor;
                CheckMajorSeventh(ref span);
            }
            else if (span.ConsumeIfStartsWith("aug"))
            {
                _triad = ChordQuality.Augmented;
                CheckMajorSeventh(ref span);
            }
            else if (span.ConsumeIfStartsWith("dim"))
            {
                _triad = ChordQuality.Diminished;
                _seventh = ChordQuality.Diminished;
                CheckMajorSeventh(ref span);
            }
            else if (span.ConsumeIfStartsWith("+"))
            {
                _triad = ChordQuality.Augmented;
                CheckMajorSeventh(ref span);
            }
            else if (span.ConsumeIfStartsWith("-"))
            {
                _triad = ChordQuality.Minor;
                CheckMajorSeventh(ref span);
            }
            else if (span.ConsumeIfStartsWith("M"))
            {
                _triad = ChordQuality.Major;
                _seventh = ChordQuality.Major;
            }
            else if (span.ConsumeIfStartsWith("m"))
            {
                _triad = ChordQuality.Minor;
                CheckMajorSeventh(ref span);
            }
        }

        private void ParseUpmostInterval(ref ReadOnlySpan<char> span)
        {
            int? intervalNull = span.ParseConsumeLeadingPositiveInt();
            if (intervalNull == 5)
            {
                if (_triad != ChordQuality.Default || _seventh != ChordQuality.Default)
                    throw new ArgumentException("Chord quality specifier is not allowed for power chords");
                _triad = ChordQuality.PowerChord;
            }
            int interval = intervalNull ?? 5;
            if (interval is < 5 or > 13)
                throw new ArgumentException($"Chord interval should be from 5 to 13, got {interval}");
            if (interval != 6 && interval % 2 == 0)
                throw new ArgumentException($"Illegal even chord interval {interval}");
            if (interval < 7 &&
                _seventh is not ChordQuality.Default and not ChordQuality.Diminished &&
                (_seventh != ChordQuality.Major || _triad != ChordQuality.Major))
                throw new ArgumentException($"Specified {_seventh} for the 7th, but the chord doesn't have a 7th");
            _upmostInterval = interval;
            ApplyStem();
        }

        private void ApplyStem()
        {
            NoteValue bass = _chord.BassNote;
            _chord.Add(bass);
            _chord.Add(bass.IntervalUp(_triad switch
            {
                ChordQuality.Default or ChordQuality.Major or ChordQuality.Augmented => 4,
                ChordQuality.Minor or ChordQuality.Diminished => 3,
                ChordQuality.PowerChord => 0,
                _ => throw new ArgumentException()
            }));
            _chord.Add(bass.IntervalUp(_triad switch
            {
                ChordQuality.Augmented => 8,
                ChordQuality.Diminished => 6,
                _ => 7
            }));
            if (_upmostInterval == 6)
                _chord.Add(bass.IntervalDown(3)); // Always major 6th
            if (_upmostInterval < 7) return;
            _chord.Add(bass.IntervalDown(_seventh switch
            {
                ChordQuality.Major => 1,
                ChordQuality.Minor or ChordQuality.Default => 2,
                ChordQuality.Diminished => 3,
                _ => throw new ArgumentException()
            }));
            if (_upmostInterval < 9) return;
            _chord.Add(bass.IntervalUp(2)); // Always major 9th
            if (_upmostInterval < 11) return;
            _chord.Add(bass.IntervalUp(5)); // Always perfect 11th
            if (_upmostInterval < 13) return;
            _chord.Add(bass.IntervalDown(3)); // Always major 13th
        }

        private void ParseAlteration(ref ReadOnlySpan<char> span)
        {
            NoteValue bass = _chord.BassNote;
            Span<bool> altered = stackalloc bool[5];

            void ApplyAlteration(ref Span<bool> altered, int interval, int accidental)
            {
                switch (interval)
                {
                    case 5 when _triad is ChordQuality.Augmented or ChordQuality.Diminished or ChordQuality.PowerChord:
                        throw new ArgumentException($"Altering 5th while 5th specified as {_triad}");
                    case 7 when _seventh is not ChordQuality.Default:
                        throw new ArgumentException($"Altering 7th while 7th specified as {_seventh}");
                }

                int index = (interval - 5) / 2;
                if (!altered[index])
                {
                    altered[index] = true;
                    _chord.Remove(bass.IntervalUp(IntervalInHalfSteps[interval]));
                }
                AddNoteChecked(bass.IntervalUp(IntervalInHalfSteps[interval] + accidental));
            }

            while (true)
            {
                span.ConsumeIfStartsWith(",");
                (int interval, int accidental) = TryParseOneInterval(ref span);
                if (interval == 0) return;
                if (interval % 2 == 0 || interval is < 5 or > 13)
                    throw new ArgumentException("Only odd intervals from 5th to 13th are allowed in alterations");
                ApplyAlteration(ref altered, interval, accidental);
            }
        }

        private void ParseSuspension(ref ReadOnlySpan<char> span)
        {
            if (!span.ConsumeIfStartsWith("sus")) return;

            NoteValue bass = _chord.BassNote;
            if (_triad is ChordQuality.Major or ChordQuality.Minor or ChordQuality.PowerChord &&
                _seventh == ChordQuality.Default)
                throw new ArgumentException($"Specified {_triad} for the triad, but the chord is suspended");
            _chord.Remove(bass.IntervalUp(
                _triad is ChordQuality.Diminished or ChordQuality.Minor ? 3 : 4));

            if (span.ConsumeIfStartsWith("2,4") ||
                span.ConsumeIfStartsWith("4,2"))
            {
                _chord.Add(bass.IntervalUp(2)); // M2
                _chord.Add(bass.IntervalUp(5)); // P4
            }
            else if (span.ConsumeIfStartsWith("2"))
                _chord.Add(bass.IntervalUp(2)); // M2
            else
            {
                span.ConsumeIfStartsWith("4"); // Consume the '4' in "sus4"
                _chord.Add(bass.IntervalUp(5)); // P4
            }
        }

        private void ParseAdditionOmission(ref ReadOnlySpan<char> span)
        {
            void ParseOmission(ref ReadOnlySpan<char> span)
            {
                NoteValue bass = _chord.BassNote;

                void OmitThird()
                {
                    if (_triad is ChordQuality.PowerChord)
                        throw new ArgumentException("Trying to omit 3rd in a power chord");
                    NoteValue third = bass.IntervalUp(_triad switch
                    {
                        ChordQuality.Default or ChordQuality.Major or ChordQuality.Augmented => 4,
                        ChordQuality.Minor or ChordQuality.Diminished => 3,
                        _ => throw new ArgumentException()
                    });
                    if (!_chord[third])
                        throw new ArgumentException("Trying to omit 3rd, but the chord doesn't have a 3rd");
                    _chord.Remove(third);
                }

                void OmitFifth()
                {
                    if (_triad is ChordQuality.PowerChord)
                        throw new ArgumentException("Trying to omit 5th in a power chord");
                    NoteValue fifth = bass.IntervalUp(_triad switch
                    {
                        ChordQuality.Default or ChordQuality.Major or ChordQuality.Minor => 7,
                        ChordQuality.Diminished => 6,
                        ChordQuality.Augmented => 8,
                        _ => throw new ArgumentException()
                    });
                    if (!_chord[fifth])
                        throw new ArgumentException("Trying to omit 5th, but the chord doesn't have a 5th");
                    _chord.Remove(fifth);
                }

                if (span.ConsumeIfStartsWith("3,5") ||
                    span.ConsumeIfStartsWith("5,3"))
                {
                    OmitThird();
                    OmitFifth();
                }
                else if (span.ConsumeIfStartsWith("5"))
                    OmitFifth();
                else if (span.ConsumeIfStartsWith("3"))
                    OmitThird();
                else
                    throw new ArgumentException("Unrecognized omission specifier, must be 3 and/or 5");
            }

            while (true)
            {
                if (span.ConsumeIfStartsWith("add"))
                {
                    ParseAddition(ref span);
                    continue;
                }
                if (span.ConsumeIfStartsWith("omit") ||
                    span.ConsumeIfStartsWith("no"))
                {
                    ParseOmission(ref span);
                    continue;
                }
                break;
            }
        }

        private void ParseParensAddition(ref ReadOnlySpan<char> span)
        {
            if (!span.ConsumeIfStartsWith("(")) return;
            ParseAddition(ref span);
            if (!span.ConsumeIfStartsWith(")"))
                throw new ArgumentException("Parenthesis not closed");
        }

        private void ParseSlash(ref ReadOnlySpan<char> span)
        {
            if (!span.ConsumeIfStartsWith("/")) return;
            NoteValue note = ParseNote(ref span);
            _chord.BassNote = note;
            _chord.Add(note);
        }

        private static NoteValue ParseNote(ref ReadOnlySpan<char> span)
        {
            if (span[0] is < 'A' or > 'G')
                throw new ArgumentException($"Invalid note name {span[0]}");
            (int accidental, int parsed) = span.ElementAtOrDefault(1) switch
            {
                '#' => (1, 2),
                'x' => (2, 2),
                'b' => span.ElementAtOrDefault(2) == 'b' ? (-2, 3) : (-1, 2),
                _ => (0, 1)
            };
            var result = CMajorNoteValues[span[0] - 'A'].IntervalUp(accidental);
            span = span[parsed..];
            return result;
        }

        private void ParseAddition(ref ReadOnlySpan<char> span)
        {
            NoteValue bass = _chord.BassNote;
            bool first = true;
            while (true)
            {
                if (!first) span.ConsumeIfStartsWith(",");
                else first = false;
                (int interval, int accidental) = TryParseOneInterval(ref span);
                if (interval == 0) return;
                AddNoteChecked(bass.IntervalUp(IntervalInHalfSteps[interval] + accidental));
            }
        }

        private (int interval, int accidental) TryParseOneInterval(ref ReadOnlySpan<char> span)
        {
            int accidental = 0;
            if (span.ConsumeIfStartsWith("b"))
                accidental = -1;
            else if (span.ConsumeIfStartsWith("#"))
                accidental = 1;
            int? interval = span.ParseConsumeLeadingPositiveInt();
            switch (interval)
            {
                case null:
                    if (accidental == 0) return (0, 0);
                    throw new ArgumentException("Unrecognized interval in add");
                case < 1 or > 15: throw new ArgumentException($"Illegal interval in add: {interval}");
            }
            return (interval.Value, accidental);
        }

        private void AddNoteChecked(NoteValue note)
        {
            if (_chord[note])
                throw new ArgumentException("The interval to add already exists in the chord");
            _chord.Add(note);
        }
    }

    private class ChordNamer
    {
        private enum Third { No, Minor, Major, Sus2, Sus4, Sus24 }
        private enum Fifth { No, Perfect, Diminished, Augmented }
        private enum Seventh { No, Minor, Major, Diminished }
        private const int Flat = 1;
        private const int Natural = 2;
        private const int Sharp = 4;

        private Chord _chord;
        private NoteValue _root;
        private Third _third;
        private Fifth _fifth;
        private Seventh _seventh;
        private int[] _extensions = new int[3]; // 9th, 11th, 13th
        private NoteValue _bass;

        private List<NoteName> _noteNames = null!;
        private string _chordName = "";

        public (string chordName, List<NoteName> noteNames) FindNames(Chord chord)
        {
            for (int i = 0; i < 12; i++)
            {
                NoteValue note = (NoteValue)i;
                if (!chord[note]) continue;
                _chord = chord;
                _root = note;
                _bass = chord.BassNote;
                Array.Fill(_extensions, 0);
                TryWithRoot();
            }
            return (_chordName, _noteNames);
        }

        private void TryWithRoot()
        {
            TryRemove(_root);
            FindThird();
            FindFifth();
            FindSeventh();
            FindExtensions();
            TryName();
        }

        private void FindThird()
        {
            if (TryRemove(_root.IntervalUp(4)))
                _third = Third.Major;
            else if (TryRemove(_root.IntervalUp(3)))
                _third = Third.Minor;
            else if (TryRemove(_root.IntervalUp(5)))
                _third = TryRemove(_root.IntervalUp(2)) ? Third.Sus24 : Third.Sus4;
            else if (TryRemove(_root.IntervalUp(2)))
                _third = Third.Sus2;
            else
                _third = Third.No;
        }

        private void FindFifth()
        {
            if (TryRemove(_root.IntervalUp(7)))
                _fifth = Fifth.Perfect;
            else if (_third is Third.Minor or Third.No && TryRemove(_root.IntervalUp(6)))
                _fifth = Fifth.Diminished;
            else if (_third is Third.Major or Third.No && TryRemove(_root.IntervalUp(8)))
                _fifth = Fifth.Augmented;
            else
                _fifth = Fifth.No;
        }

        private void FindSeventh()
        {
            if (TryRemove(_root.IntervalDown(1)))
                _seventh = Seventh.Major;
            else if (TryRemove(_root.IntervalDown(2)))
                _seventh = Seventh.Minor;
            else if (_third == Third.Minor &&
                     _fifth == Fifth.Diminished &&
                     TryRemove(_root.IntervalDown(3)))
                _seventh = Seventh.Diminished;
            else
                _seventh = Seventh.No;
        }

        private void FindExtensions()
        {
            Span<int> naturalSize = stackalloc int[] { 2, 5, 9 };
            for (int i = 0; i < 3; i++)
            {
                if (TryRemove(_root.IntervalUp(naturalSize[i] - 1)))
                    _extensions[i] |= Flat;
                if (TryRemove(_root.IntervalUp(naturalSize[i])))
                    _extensions[i] |= Natural;
                if (TryRemove(_root.IntervalUp(naturalSize[i] + 1)))
                    _extensions[i] |= Sharp;
            }
        }

        private void TryName()
        {
            (NoteName root, NoteName bass) = FindRootAndBassNoteNames();
            StringBuilder sb = new(root.ToString());
            int stemDeg = GetStemDegree();
            AppendChordStem(sb, stemDeg);
            AppendExtensions(sb, stemDeg);
            if (bass != root)
            {
                sb.Append('/');
                sb.Append(bass);
            }
            string result = sb.ToString();
            if (_chordName != "" && result.Length >= _chordName.Length) return;
            _chordName = result;
            _noteNames = FindAllNoteNames(root);
        }

        private (NoteName root, NoteName bass) FindRootAndBassNoteNames()
        {
            NoteName rootNote;

            if ((int)_root is 1 or 3 or 6 or 8 or 10) // Black keys
            {
                NoteBase lower = (NoteBase)((int)_root / 2);
                NoteName sharpName = new(lower, 1);
                NoteName flatName = new((NoteBase)((int)lower + 1), -1);

                int AccidentalCount(NoteName candidate)
                {
                    int count = 0;
                    foreach (NoteName n in IterateChordNotes(candidate))
                    {
                        if (n.Accidental is < -2 or > 2) return 100;
                        count += n.Accidental == 0 ? 0 : 1;
                    }
                    return count;
                }

                rootNote = AccidentalCount(sharpName) > AccidentalCount(flatName)
                    ? flatName
                    : sharpName;
            }
            else
                rootNote = new NoteName((NoteBase)(((int)_root + 1) / 2), 0);

            NoteName bassNote = new();
            foreach (NoteName n in IterateChordNotes(rootNote))
                if (n.Value == _bass)
                {
                    bassNote = n;
                    break;
                }

            return (rootNote, bassNote);
        }

        private IEnumerable<NoteName> IterateChordNotes(NoteName note)
        {
            yield return note;
            // @formatter:off
            switch (_third)
            {
                case Third.No: break;
                case Third.Minor: yield return note.IntervalUp(3, 3); break;
                case Third.Major: yield return note.IntervalUp(4, 3); break;
                case Third.Sus2: yield return note.IntervalUp(2, 2); break;
                case Third.Sus4: yield return note.IntervalUp(5, 4); break;
                case Third.Sus24:
                    yield return note.IntervalUp(2, 2);
                    yield return note.IntervalUp(5, 4);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
            switch (_fifth)
            {
                case Fifth.No: break;
                case Fifth.Perfect: yield return note.IntervalUp(7, 5); break;
                case Fifth.Diminished: yield return note.IntervalUp(6, 5); break;
                case Fifth.Augmented: yield return note.IntervalUp(8, 5); break;
                default: throw new ArgumentOutOfRangeException();
            }
            switch (_seventh)
            {
                case Seventh.No: break;
                case Seventh.Minor: yield return note.IntervalUp(10, 7); break;
                case Seventh.Major: yield return note.IntervalUp(11, 7); break;
                case Seventh.Diminished: yield return note.IntervalUp(9, 7); break;
                default: throw new ArgumentOutOfRangeException();
            }
            // @formatter:on
            int[] extensionHalfSteps = { 2, 5, 9 };
            for (int i = 0; i < 3; i++)
            {
                if ((_extensions[i] & Flat) != 0)
                    yield return note.IntervalUp(extensionHalfSteps[i] - 1, i * 2 + 9);
                if ((_extensions[i] & Natural) != 0)
                    yield return note.IntervalUp(extensionHalfSteps[i], i * 2 + 9);
                if ((_extensions[i] & Sharp) != 0)
                    yield return note.IntervalUp(extensionHalfSteps[i] + 1, i * 2 + 9);
            }
        }

        private void AppendChordStem(StringBuilder sb, int deg)
        {
            string degStr = _seventh switch
            {
                Seventh.Minor => deg.ToString(),
                Seventh.Major => $"M{deg}",
                _ => ""
            };
            sb.Append((_third, _fifth, _seventh) switch
            {
                (Third.No, Fifth.No, _) => $"{degStr}omit3,5",
                (Third.No, Fifth.Perfect, Seventh.No) => "5",
                (Third.No, Fifth.Diminished, Seventh.No) => ",b5omit3",
                (Third.No, Fifth.Augmented, Seventh.No) => ",#5omit3",
                (Third.No, Fifth.Perfect, _) => $"{degStr}omit3",
                (Third.Minor, Fifth.Diminished, Seventh.No) => "dim",
                (Third.Minor, Fifth.Diminished, Seventh.Minor) => $"m{degStr}b5",
                (Third.Major, Fifth.Augmented, Seventh.No or Seventh.Minor) => $"aug{degStr}",
                (_, _, Seventh.Diminished) => $"dim{deg}",
                _ => GeneralNotation(degStr, _third, _fifth)
            });
        }

        private static string GeneralNotation(string deg, Third third, Fifth fifth)
        {
            string fifthStr = fifth switch
            {
                Fifth.No => "omit5",
                Fifth.Perfect => "",
                Fifth.Diminished => "b5",
                Fifth.Augmented => "#5",
                _ => throw new ArgumentOutOfRangeException(nameof(fifth), fifth, null)
            };
            if (deg == "" &&
                third is Third.Sus2 or Third.Sus4 or Third.Sus24 &&
                fifth is Fifth.Diminished or Fifth.Augmented) deg = ","; // Disambiguate C,#5 from C#5
            return third switch
            {
                Third.No => $"{deg}{fifthStr}omit3",
                Third.Major => deg + fifthStr,
                Third.Minor => $"m{deg}{fifthStr}",
                Third.Sus2 => $"{deg}{fifthStr}sus2",
                Third.Sus4 => $"{deg}{fifthStr}sus",
                Third.Sus24 => $"{deg}{fifthStr}sus2,4",
                _ => throw new ArgumentOutOfRangeException(nameof(third), third, null)
            };
        }

        private int GetStemDegree()
        {
            if (_seventh == Seventh.No) return 5;
            int result = 7;
            for (int i = 0; i < 3; i++)
            {
                if ((_extensions[i] & Natural) == 0) break;
                result += 2;
            }
            return result;
        }

        private void AppendExtensions(StringBuilder sb, int stemDegree)
        {
            bool first = true;
            int start = stemDegree > 7 ? (stemDegree - 7) / 2 : 0;
            for (int i = 0; i < 3; i++)
            {
                if ((_extensions[i] & Flat) != 0)
                {
                    sb.Append(first ? '(' : ',');
                    first = false;
                    sb.Append('b');
                    sb.Append(i * 2 + 9);
                }
                if (i >= start && (_extensions[i] & Natural) != 0)
                {
                    sb.Append(first ? '(' : ',');
                    first = false;
                    sb.Append(i * 2 + 9);
                }
                if ((_extensions[i] & Sharp) != 0)
                {
                    sb.Append(first ? '(' : ',');
                    first = false;
                    sb.Append('#');
                    sb.Append(i * 2 + 9);
                }
            }
            if (!first) sb.Append(')');
        }

        private List<NoteName> FindAllNoteNames(NoteName root)
        {
            List<NoteName> result = [root];
            // @formatter:off
            switch (_third)
            {
                case Third.No: break;
                case Third.Minor: result.Add(root.IntervalUp(3, 3)); break;
                case Third.Major: result.Add(root.IntervalUp(4, 3)); break;
                case Third.Sus2: result.Add(root.IntervalUp(2, 2)); break;
                case Third.Sus4: result.Add(root.IntervalUp(5, 4)); break;
                case Third.Sus24: result.Add(root.IntervalUp(2, 2)); result.Add(root.IntervalUp(5, 4)); break;
                default: throw new ArgumentOutOfRangeException();
            }
            switch (_fifth)
            {
                case Fifth.No: break;
                case Fifth.Perfect: result.Add(root.IntervalUp(7, 5)); break;
                case Fifth.Diminished: result.Add(root.IntervalUp(6, 5)); break;
                case Fifth.Augmented: result.Add(root.IntervalUp(8, 5)); break;
                default: throw new ArgumentOutOfRangeException();
            }
            switch (_seventh)
            {
                case Seventh.No: break;
                case Seventh.Minor: result.Add(root.IntervalUp(10, 7)); break;
                case Seventh.Major: result.Add(root.IntervalUp(11, 7)); break;
                case Seventh.Diminished: result.Add(root.IntervalUp(9, 7)); break;
                default: throw new ArgumentOutOfRangeException();
            }
            // @formatter:on
            Span<int> naturalSize = stackalloc int[] { 2, 5, 9 };
            for (int i = 0; i < 3; i++)
            {
                if ((_extensions[i] & Flat) != 0)
                    result.Add(root.IntervalUp(naturalSize[i] - 1, 9 + 2 * i));
                if ((_extensions[i] & Natural) != 0)
                    result.Add(root.IntervalUp(naturalSize[i], 9 + 2 * i));
                if ((_extensions[i] & Sharp) != 0)
                    result.Add(root.IntervalUp(naturalSize[i] + 1, 9 + 2 * i));
            }
            return result;
        }

        private bool TryRemove(NoteValue note)
        {
            if (!_chord[note]) return false;
            _chord.Remove(note);
            return true;
        }
    }
}
