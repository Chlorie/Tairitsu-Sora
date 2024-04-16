namespace TairitsuSora.Commands.Chess;

public static class Extensions
{
    public static Color Opponent(this Color color) => color == Color.White ? Color.Black : Color.White;

    public static bool HasKingSide(this CastlingRight right) => ((byte)right & KingSideMask) != 0;
    public static bool HasQueenSide(this CastlingRight right) => ((byte)right & QueenSideMask) != 0;
    public static CastlingRight AddKingSide(this CastlingRight right) => (CastlingRight)((byte)right | KingSideMask);
    public static CastlingRight AddQueenSide(this CastlingRight right) => (CastlingRight)((byte)right | QueenSideMask);

    public static CastlingRight RemoveKingSide(this CastlingRight right) =>
        (CastlingRight)((byte)right & QueenSideMask);

    public static CastlingRight RemoveQueenSide(this CastlingRight right) =>
        (CastlingRight)((byte)right & KingSideMask);

    private const byte KingSideMask = (byte)CastlingRight.KingSide;
    private const byte QueenSideMask = (byte)CastlingRight.QueenSide;
}
