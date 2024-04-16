namespace TairitsuSora.Commands.Chess;

public readonly record struct Coords(int Index)
{
    public int Rank => Index / 8;
    public int File => Index % 8;

    public Coords(int rank, int file) : this(rank * 8 + file) { }

    public static Coords Parse(ReadOnlySpan<char> repr)
    {
        if (repr.Length != 2)
            throw new ArgumentException("Coordinates must be 2 characters long", nameof(repr));
        if (repr[0] is < 'a' or > 'h')
            throw new ArgumentException("File must be between 'a' and 'h'", nameof(repr));
        if (repr[1] is < '1' or > '8')
            throw new ArgumentException("Rank must be between '1' and '8'", nameof(repr));
        return new Coords(repr[1] - '1', repr[0] - 'a');
    }

    public static Coords? TryParse(ReadOnlySpan<char> repr)
    {
        if (repr.Length != 2 || repr[0] is < 'a' or > 'h' || repr[1] is < '1' or > '8')
            return null;
        return new Coords(repr[1] - '1', repr[0] - 'a');
    }

    public override string ToString() => $"{(char)('a' + File)}{Rank + 1}";
}

public enum CastlingRight : byte { None, KingSide, QueenSide, Both }

public enum MoveType : byte
{
    None,
    EnPassant,
    PKnight, PBishop, PRook, PQueen, // Same representation as the piece types
    ShortCastle, LongCastle,
    PawnTwoSquares
}

public readonly struct Move(Coords src, Coords dst, MoveType type = MoveType.None) : IEquatable<Move>
{
    public Coords Src => new(_data & 0x3f);
    public Coords Dst => new((_data >> 6) & 0x3f);

    public MoveType Type
    {
        get => (MoveType)(_data >> 12);
        init
        {
            _data &= 0xf000;
            _data |= (ushort)((ushort)value << 12);
        }
    }

    public bool Equals(Move other) => _data == other._data;
    public override bool Equals(object? obj) => obj is Move other && Equals(other);
    public static bool operator ==(Move left, Move right) => left.Equals(right);
    public static bool operator !=(Move left, Move right) => !left.Equals(right);

    public override int GetHashCode() => _data.GetHashCode();

    // [move type: 4] [destination: 6] [source: 6]
    private readonly ushort _data = (ushort)(src.Index | (dst.Index << 6) | ((int)type << 12));
}
