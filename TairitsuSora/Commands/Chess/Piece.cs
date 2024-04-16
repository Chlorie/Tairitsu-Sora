namespace TairitsuSora.Commands.Chess;

public enum Color : byte { White = 0x0, Black = 0x8 }
public enum PieceType : byte { None, Pawn, Knight, Bishop, Rook, Queen, King }

public struct Piece : IEquatable<Piece>
{
    public static readonly Piece None = new(Color.White, PieceType.None);
    public static readonly Piece WhitePawn = new(Color.White, PieceType.Pawn);
    public static readonly Piece WhiteKnight = new(Color.White, PieceType.Knight);
    public static readonly Piece WhiteBishop = new(Color.White, PieceType.Bishop);
    public static readonly Piece WhiteRook = new(Color.White, PieceType.Rook);
    public static readonly Piece WhiteQueen = new(Color.White, PieceType.Queen);
    public static readonly Piece WhiteKing = new(Color.White, PieceType.King);
    public static readonly Piece BlackPawn = new(Color.Black, PieceType.Pawn);
    public static readonly Piece BlackKnight = new(Color.Black, PieceType.Knight);
    public static readonly Piece BlackBishop = new(Color.Black, PieceType.Bishop);
    public static readonly Piece BlackRook = new(Color.Black, PieceType.Rook);
    public static readonly Piece BlackQueen = new(Color.Black, PieceType.Queen);
    public static readonly Piece BlackKing = new(Color.Black, PieceType.King);

    public readonly Color Color => (Color)(_data & 0x8);
    public readonly PieceType Type => (PieceType)(_data & 0x7);

    public Piece(byte data) => _data = data;
    public Piece(Color color, PieceType type) => _data = (byte)((byte)color | (byte)type);

    public readonly bool Equals(Piece other) => _data == other._data;
    public readonly override bool Equals(object? obj) => obj is Piece other && Equals(other);
    public static bool operator ==(Piece left, Piece right) => left.Equals(right);
    public static bool operator !=(Piece left, Piece right) => !left.Equals(right);
    public readonly override int GetHashCode() => _data.GetHashCode();

    public static Piece operator |(Piece lhs, Piece rhs) => new((byte)(lhs._data | rhs._data));

    public static Piece FromFen(char ch) =>
        ch switch
        {
            'P' => WhitePawn,
            'N' => WhiteKnight,
            'B' => WhiteBishop,
            'R' => WhiteRook,
            'Q' => WhiteQueen,
            'K' => WhiteKing,
            'p' => BlackPawn,
            'n' => BlackKnight,
            'b' => BlackBishop,
            'r' => BlackRook,
            'q' => BlackQueen,
            'k' => BlackKing,
            _ => None
        };

    public readonly char FenRepr => (Color, Type) switch
    {
        (Color.White, PieceType.Pawn) => 'P',
        (Color.White, PieceType.Knight) => 'N',
        (Color.White, PieceType.Bishop) => 'B',
        (Color.White, PieceType.Rook) => 'R',
        (Color.White, PieceType.Queen) => 'Q',
        (Color.White, PieceType.King) => 'K',
        (Color.Black, PieceType.Pawn) => 'p',
        (Color.Black, PieceType.Knight) => 'n',
        (Color.Black, PieceType.Bishop) => 'b',
        (Color.Black, PieceType.Rook) => 'r',
        (Color.Black, PieceType.Queen) => 'q',
        (Color.Black, PieceType.King) => 'k',
        _ => throw new ArgumentOutOfRangeException()
    };

    private byte _data;
}
