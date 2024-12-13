namespace TairitsuSora.Commands.Othello;

public readonly record struct Board(
    ulong Black, ulong White, ulong LegalMoves,
    Board.CellType ActivePlayer = Board.CellType.Black,
    bool Ended = false)
{
    public enum CellType { None, Black, White }

    public readonly record struct Coords(int Rank, int File)
    {
        public override string ToString() => $"{(char)('A' + File)}{Rank + 1}";

        public static Coords FromString(ReadOnlySpan<char> value)
        {
            if (value.Length != 2)
                throw new ArgumentException("Coordinate representations should be of length 2");
            char file = char.ToLower(value[0]);
            if (file is < 'a' or > 'h')
                throw new ArgumentException($"Rank should be from A to H, but got {value[0]}");
            if (value[1] is < '1' or > '8')
                throw new ArgumentException($"File should be from 1 to 8, but got {value[1]}");
            return new Coords(value[1] - '1', file - 'a');
        }

        public static Coords? TryFromString(ReadOnlySpan<char> value)
        {
            if (value.Length != 2 || value[1] is < '1' or > '8') return null;
            char file = char.ToLower(value[0]);
            if (file is < 'a' or > 'h') return null;
            return new Coords(value[1] - '1', file - 'a');
        }

        public static Coords FromIndex(int index) => new(index / 8, index % 8);

        public int ToIndex() => Rank * 8 + File;
    }

    public CellType this[int rank, int file]
    {
        get
        {
            ulong bit = 1ul << (rank * 8 + file);
            return (Black & bit) != 0 ? CellType.Black
                 : (White & bit) != 0 ? CellType.White
                                      : CellType.None;
        }
    }

    public CellType this[Coords coords] => this[coords.Rank, coords.File];

    public (int black, int white) CountDisks(bool ended)
    {
        int black = (int)ulong.PopCount(Black), white = (int)ulong.PopCount(White);
        if (!ended) return (black, white);
        return black == white ? (black, white)
              : black > white ? (64 - white, white)
                              : (black, 64 - black);
    }

    public int Mobility => (int)ulong.PopCount(LegalMoves);
    public int TotalDisks => (int)ulong.PopCount(Black) + (int)ulong.PopCount(White);

    public bool PlayableAt(Coords? coords) =>
        coords is { } c ? (LegalMoves & 1ul << c.ToIndex()) != 0 : LegalMoves == 0;
}
