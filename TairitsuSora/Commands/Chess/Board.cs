using System.Diagnostics.CodeAnalysis;

namespace TairitsuSora.Commands.Chess;

public struct HalfBoard : IEquatable<HalfBoard>
{
    public ulong Pawn { get; private set; }
    public ulong Knight { get; private set; }
    public ulong Bishop { get; private set; }
    public ulong Rook { get; private set; }
    public ulong Queen { get; private set; }
    public ulong King { get; private set; }

    public readonly ulong AnyPiece => Pawn | Knight | Bishop | Rook | Queen | King;

    public PieceType this[Coords coords]
    {
        readonly get
        {
            ulong bit = 1ul << coords.Index;
            return (Pawn & bit) != 0 ? PieceType.Pawn :
                (Knight & bit) != 0 ? PieceType.Knight :
                (Bishop & bit) != 0 ? PieceType.Bishop :
                (Rook & bit) != 0 ? PieceType.Rook :
                (Queen & bit) != 0 ? PieceType.Queen :
                (King & bit) != 0 ? PieceType.King :
                PieceType.None;
        }
        set
        {
            ulong bit = 1ul << coords.Index;
            ulong invert = ~bit;
            Pawn &= invert;
            Knight &= invert;
            Bishop &= invert;
            Rook &= invert;
            Queen &= invert;
            King &= invert;
            switch (value)
            {
                case PieceType.None: break;
                case PieceType.Pawn: Pawn |= bit; break;
                case PieceType.Knight: Knight |= bit; break;
                case PieceType.Bishop: Bishop |= bit; break;
                case PieceType.Rook: Rook |= bit; break;
                case PieceType.Queen: Queen |= bit; break;
                case PieceType.King: King |= bit; break;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown PieceType");
            }
        }
    }

    public static HalfBoard StartingPositionWhite => new()
    {
        Pawn = 0xff00ul,
        Knight = 0x42ul,
        Bishop = 0x24ul,
        Rook = 0x81ul,
        Queen = 0x8ul,
        King = 0x10ul
    };

    public static HalfBoard StartingPositionBlack => new()
    {
        Pawn = 0xff_0000_0000_0000ul,
        Knight = 0x4200_0000_0000_0000ul,
        Bishop = 0x2400_0000_0000_0000ul,
        Rook = 0x8100_0000_0000_0000ul,
        Queen = 0x800_0000_0000_0000ul,
        King = 0x1000_0000_0000_0000ul
    };

    public readonly bool Equals(HalfBoard other) =>
        Pawn == other.Pawn && Knight == other.Knight && Bishop == other.Bishop &&
        Rook == other.Rook && Queen == other.Queen && King == other.King;
    public readonly override bool Equals(object? obj) => obj is HalfBoard other && Equals(other);
    public static bool operator ==(HalfBoard left, HalfBoard right) => left.Equals(right);
    public static bool operator !=(HalfBoard left, HalfBoard right) => !left.Equals(right);

    public readonly override int GetHashCode() => HashCode.Combine(Pawn, Knight, Bishop, Rook, Queen, King);
}

public struct Board : IEquatable<Board>
{
    public readonly HalfBoard White => _white;
    public readonly HalfBoard Black => _black;

    public Piece this[Coords coords]
    {
        readonly get
        {
            PieceType black = Black[coords];
            return black != PieceType.None ?
                new Piece(Color.Black, black) :
                new Piece(Color.White, White[coords]);
        }
        set
        {
            ref HalfBoard place = ref (value.Color == Color.White ? ref _white : ref _black);
            ref HalfBoard clear = ref (value.Color == Color.White ? ref _black : ref _white);
            place[coords] = value.Type;
            clear[coords] = PieceType.None;
        }
    }

    public static Board StartingPosition => new()
    { _white = HalfBoard.StartingPositionWhite, _black = HalfBoard.StartingPositionBlack };

    public readonly bool Equals(Board other) => _white.Equals(other._white) && _black.Equals(other._black);
    public readonly override bool Equals(object? obj) => obj is Board other && Equals(other);
    public static bool operator ==(Board left, Board right) => left.Equals(right);
    public static bool operator !=(Board left, Board right) => !left.Equals(right);

    public readonly override int GetHashCode() => HashCode.Combine(_white, _black);

    private HalfBoard _white;
    private HalfBoard _black;
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class BitBoardManip
{
    public static ulong KnightMove(ulong bits)
    {
        ulong lr1 = ((bits & NotAFile) >> 1) | ((bits & NotHFile) << 1);
        ulong lr1ud2 = (lr1 >> 16) | (lr1 << 16);
        ulong ud1 = (bits >> 8) | (bits << 8);
        ulong ud1lr2 = ((ud1 & NotABFile) >> 2) | ((ud1 & NotGHFile) << 2);
        return lr1ud2 | ud1lr2;
    }

    public static ulong KingMove(ulong bits)
    {
        ulong res = bits;
        res |= ((res & NotAFile) >> 1) | ((res & NotHFile) << 1);
        res |= (res >> 8) | (res << 8);
        res &= ~bits;
        return res;
    }

    public static ulong BishopMove(ulong bits, ulong empty) =>
        BishopMoveLD(bits, empty) | BishopMoveRD(bits, empty) | BishopMoveRU(bits, empty) | BishopMoveLU(bits, empty);

    public static ulong RookMove(ulong bits, ulong empty) =>
        RookMoveL(bits, empty) | RookMoveD(bits, empty) | RookMoveR(bits, empty) | RookMoveU(bits, empty);

    public static ulong WhitePawnAttacked(ulong bits) => ((bits & NotAFile) << 7) | ((bits & NotHFile) << 9);
    public static ulong BlackPawnAttacked(ulong bits) => ((bits & NotAFile) >> 9) | ((bits & NotHFile) >> 7);

    public static ulong WhitePawnForward(ulong bits, ulong empty) => (bits << 8) & empty;
    public static ulong BlackPawnForward(ulong bits, ulong empty) => (bits >> 8) & empty;
    public static ulong WhitePawnTwoSquares(ulong bits, ulong empty) => ((((bits & Rank2) << 8) & empty) << 8) & empty;
    public static ulong BlackPawnTwoSquares(ulong bits, ulong empty) => ((((bits & Rank7) >> 8) & empty) >> 8) & empty;

    private static ulong Dumb7Shl(ulong bits, int shift, ulong mask, ulong empty)
    {
        while (true)
        {
            ulong added = ((bits & mask) << shift) & empty;
            if ((added & bits) == added) return (bits & mask) << shift;
            bits |= added;
        }
    }

    private static ulong Dumb7Shr(ulong bits, int shift, ulong mask, ulong empty)
    {
        while (true)
        {
            ulong added = ((bits & mask) >> shift) & empty;
            if ((added & bits) == added) return (bits & mask) >> shift;
            bits |= added;
        }
    }

    private static ulong BishopMoveLD(ulong bits, ulong empty) => Dumb7Shr(bits, 9, NotAFile, empty);
    private static ulong BishopMoveRD(ulong bits, ulong empty) => Dumb7Shr(bits, 7, NotHFile, empty);
    private static ulong BishopMoveRU(ulong bits, ulong empty) => Dumb7Shl(bits, 9, NotHFile, empty);
    private static ulong BishopMoveLU(ulong bits, ulong empty) => Dumb7Shl(bits, 7, NotAFile, empty);

    private static ulong RookMoveL(ulong bits, ulong empty) => Dumb7Shr(bits, 1, NotAFile, empty);
    private static ulong RookMoveD(ulong bits, ulong empty) => Dumb7Shr(bits, 8, ~0ul, empty);
    private static ulong RookMoveR(ulong bits, ulong empty) => Dumb7Shl(bits, 1, NotHFile, empty);
    private static ulong RookMoveU(ulong bits, ulong empty) => Dumb7Shl(bits, 8, ~0ul, empty);

    private const ulong NotAFile = 0xfefefefefefefefeul;
    private const ulong NotHFile = 0x7f7f7f7f7f7f7f7ful;
    private const ulong NotABFile = 0xfcfcfcfcfcfcfcfcul;
    private const ulong NotGHFile = 0x3f3f3f3f3f3f3f3ful;
    private const ulong Rank2 = 0x000000000000ff00ul;
    private const ulong Rank7 = 0x00ff000000000000ul;
}
