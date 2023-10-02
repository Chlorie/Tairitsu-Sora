namespace TairitsuSora.Commands.Music;

public static class NoteValueExtensions
{
    public static NoteValue IntervalUp(this NoteValue note, int halfSteps) => FromInt((int)note + halfSteps);
    public static NoteValue IntervalDown(this NoteValue note, int halfSteps) => FromInt((int)note - halfSteps);

    private static NoteValue FromInt(int value)
    {
        int r = value % 12;
        return (NoteValue)(r < 0 ? r + 12 : r);
    }

    public static Chord MakeChord(this NoteValue bass, params int[] intervals)
    {
        Chord chord = new(bass) { bass };
        foreach (int i in intervals) chord.Add(bass.IntervalUp(i));
        return chord;
    }

    public static Chord Maj(this NoteValue note) => note.MakeChord(4, 7);
    public static Chord Min(this NoteValue note) => note.MakeChord(3, 7);
    public static Chord Aug(this NoteValue note) => note.MakeChord(4, 8);
    public static Chord Dim(this NoteValue note) => note.MakeChord(3, 6);
    public static Chord Sus(this NoteValue note) => note.MakeChord(5, 7);
    public static Chord Sus2(this NoteValue note) => note.MakeChord(2, 7);
    public static Chord Dom7(this NoteValue note) => note.MakeChord(4, 7, 10);
    public static Chord Dom7Sus(this NoteValue note) => note.MakeChord(5, 7, 10);
    public static Chord Maj7(this NoteValue note) => note.MakeChord(4, 7, 11);
    public static Chord Min7(this NoteValue note) => note.MakeChord(3, 7, 10);
    public static Chord MinMaj7(this NoteValue note) => note.MakeChord(3, 7, 11);
    public static Chord Min7Flat5(this NoteValue note) => note.MakeChord(3, 6, 10);
    public static Chord Dim7(this NoteValue note) => note.MakeChord(3, 6, 9);
    public static Chord Aug7(this NoteValue note) => note.MakeChord(4, 8, 10);
    public static Chord Maj7Sharp5(this NoteValue note) => note.MakeChord(4, 8, 11);
}
