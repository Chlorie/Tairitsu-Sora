using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TairitsuSora.Commands.Chess;
using static System.Numerics.BitOperations;
using static BitBoardManip;

public class Game
{
    public enum Outcome
    {
        None,
        Illegal,
        DrawByRepetition,
        DrawBy50MoveRule,
        DrawByStalemate,
        WhiteWin,
        BlackWin
    }

    public Board Position => _state.Board;
    public Color Player => _state.Player;
    public int HalfMoves => _state.HalfMoves;
    public int FullMoves => _state.FullMoves;

    public static bool MatchMoveSyntax(string notation) =>
        CastleRegex.IsMatch(notation) || NormalNotationRegex.IsMatch(notation);

    public Move ParseMove(string notation)
    {
        if (string.IsNullOrEmpty(notation))
            throw new NotAMoveException();
        if (ParseCastling(notation) is { } move) return move;
        return ParseNormalMove(notation);
    }

    public List<Move> GetLegalMoves() => _state.GetLegalMoves();

    public Outcome PlayMove(Move move)
    {
        List<Move> moves = _state.GetLegalMoves();
        Move noTypeMove = move with { Type = MoveType.None };
        if (moves.All(m => m with { Type = MoveType.None } != noTypeMove)) return Outcome.Illegal;

        _50MoveCounter++;
        if (_state.Board[move.Src].Type == PieceType.Pawn || MoveCaptures(move)) _50MoveCounter = 0;
        _state.PlayMoveUnchecked(move);

        if (_state.GetLegalMoves().Count == 0) // No legal moves
            return (_state.Player, _state.IsUnderCheck()) switch
            {
                (_, false) => Outcome.DrawByStalemate,
                (Color.White, true) => Outcome.BlackWin,
                (Color.Black, true) => Outcome.WhiteWin,
                _ => throw new ArgumentOutOfRangeException()
            };

        if (_50MoveCounter == 100) return Outcome.DrawBy50MoveRule;
        var pos = (_state.Board, _state.Player);
        return _reachedBoards.TryAdd(pos, 1) ? Outcome.None :
            ++_reachedBoards[pos] == 3 ? Outcome.DrawByRepetition : Outcome.None;
    }

    public string NotateMove(Move move)
    {
        string check = VerifyMoveChecks(move) switch
        {
            CheckState.None => "",
            CheckState.Check => "+",
            CheckState.Checkmate => "#",
            _ => throw new ArgumentOutOfRangeException()
        };

        if (move.Type is MoveType.ShortCastle or MoveType.LongCastle)
            return (move.Type == MoveType.ShortCastle ? "O-O" : "O-O-O") + check;

        char file = (char)('a' + move.Src.File), rank = (char)('1' + move.Src.Rank);
        bool taking = MoveCaptures(move);

        if (_state.Board[move.Src].Type == PieceType.Pawn)
        {
            string promotion = move.Type switch
            {
                MoveType.PKnight => "=N",
                MoveType.PBishop => "=B",
                MoveType.PRook => "=R",
                MoveType.PQueen => "=Q",
                _ => ""
            };
            string stem = taking ? $"{file}x{move.Dst}" : move.Dst.ToString();
            return stem + promotion + check;
        }

        PieceType piece = _state.Board[move.Src].Type;
        List<Move> filtered = _state
            .GetLegalMoves()
            .Where(m => m.Dst == move.Dst && _state.Board[m.Src].Type == piece)
            .ToList();
        string srcClarify =
            filtered.Count == 1 ? "" :
            filtered.Count(m => m.Src.File == move.Src.File) == 1 ? file.ToString() :
            filtered.Count(m => m.Src.Rank == move.Src.Rank) == 1 ? rank.ToString() :
            move.Src.ToString();
        string takingStr = taking ? "x" : "";
        return $"{PieceCharacter(piece)}{srcClarify}{takingStr}{move.Dst}{check}";
    }

    public static Game FromStartingPosition() => new() { _state = State.FromStartingPosition() };

    public static Game FromFen(string fen) => new() { _state = State.FromFen(fen) };

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
    private struct State
    {
        public Board Board;
        public Color Player;
        public CastlingRight WhiteCastling;
        public CastlingRight BlackCastling;
        public ulong EnPassantMask;
        public int HalfMoves;
        public int FullMoves;

        public readonly List<Move> GetLegalMoves()
        {
            List<Move> candidates = GenerateCandidateMoves();
            List<Move> res = [];
            foreach (Move move in candidates)
            {
                State state = this;
                state.PlayMoveUnchecked(move, true);
                if (!state.IsUnderCheck()) res.Add(move);
            }
            return res;
        }

        public void PlayMoveUnchecked(Move move, bool onlyModifyBoard = false)
        {
            Coords src = move.Src, dst = move.Dst;
            Board[dst] = Board[src];

            switch (move.Type)
            {
                case MoveType.None:
                case MoveType.PawnTwoSquares:
                    break;

                case MoveType.EnPassant:
                    int takenPawn = TrailingZeroCount(EnPassantMask) + (Player == Color.White ? -8 : 8);
                    Board[new Coords(takenPawn)] = Piece.None;
                    break;

                case MoveType.PKnight:
                case MoveType.PBishop:
                case MoveType.PRook:
                case MoveType.PQueen:
                    Board[dst] = new Piece(Player, (PieceType)move.Type);
                    break;

                case MoveType.ShortCastle:
                    Board[new Coords(src.Index + 3)] = Piece.None;
                    Board[new Coords(src.Index + 1)] = new Piece(Player, PieceType.Rook);
                    break;

                case MoveType.LongCastle:
                    Board[new Coords(src.Index - 4)] = Piece.None;
                    Board[new Coords(src.Index - 2)] = new Piece(Player, PieceType.Rook);
                    break;

                default: throw new ArgumentOutOfRangeException();
            }

            Board[src] = Piece.None;
            if (onlyModifyBoard) return; // This is for move legality tests

            switch (src)
            {
                case (Index: 0):
                    WhiteCastling = WhiteCastling.RemoveQueenSide();
                    break; // a1
                case (Index: 4):
                    WhiteCastling = CastlingRight.None;
                    break; // e1
                case (Index: 7):
                    WhiteCastling = WhiteCastling.RemoveKingSide();
                    break; // h1
                case (Index: 56):
                    BlackCastling = BlackCastling.RemoveQueenSide();
                    break; // a8
                case (Index: 60):
                    BlackCastling = CastlingRight.None;
                    break; // e8
                case (Index: 63):
                    BlackCastling = BlackCastling.RemoveKingSide();
                    break; // h8
            }

            EnPassantMask = move.Type == MoveType.PawnTwoSquares ? 1ul << ((src.Index + dst.Index) / 2) : 0;
            HalfMoves++;
            Player = Player.Opponent();
            if (Player == Color.White) FullMoves++;
        }

        public readonly bool IsUnderCheck()
        {
            ulong empty = ~(Board.White.AnyPiece | Board.Black.AnyPiece);
            HalfBoard self = Player == Color.White ? Board.White : Board.Black;
            HalfBoard opponent = Player == Color.White ? Board.Black : Board.White;
            ulong attacked =
                (Player == Color.White ? BlackPawnAttacked(opponent.Pawn) : WhitePawnAttacked(opponent.Pawn)) |
                KnightMove(opponent.Knight) |
                BishopMove(opponent.Bishop | opponent.Queen, empty) |
                RookMove(opponent.Rook | opponent.Queen, empty) |
                KingMove(opponent.King);
            return (self.King & attacked) != 0;
        }

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static State FromStartingPosition() => new()
        {
            Board = Board.StartingPosition,
            WhiteCastling = CastlingRight.Both,
            BlackCastling = CastlingRight.Both
        };

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static State FromFen(string fen)
        {
            State game = new();
            string[] components = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (components.Length is not 4 and not 6)
                throw new ArgumentException("Number of components should be 4 or 6");
            game.ParseFenPieces(components[0]);
            game.ParseFenActivePlayer(components[1]);
            game.ParseFenCastlingRight(components[2]);
            game.EnPassantMask = components[3] == "-" ? 0ul : 1ul << Coords.Parse(components[3]).Index;
            if (components.Length != 6) return game;
            game.HalfMoves = ParseFenMoveCount(components[4]);
            game.FullMoves = ParseFenMoveCount(components[5]);
            return game;
        }

        private readonly List<Move> GenerateCandidateMoves()
        {
            HalfBoard self = Player == Color.White ? Board.White : Board.Black;
            HalfBoard opponent = Player == Color.White ? Board.Black : Board.White;
            ulong anySelf = self.AnyPiece, anyOpponent = opponent.AnyPiece, empty = ~(anySelf | anyOpponent);

            List<Move> moves = Player == Color.White
                ? GenerateWhitePawnMoves(self.Pawn, anyOpponent, empty)
                : GenerateBlackPawnMoves(self.Pawn, anyOpponent, empty);

            ulong knight = self.Knight;
            while (knight != 0)
            {
                int i = TrailingZeroCount(knight);
                AddMoves(new Coords(i), KnightMove(1ul << i) & ~anySelf);
                knight &= knight - 1;
            }

            ulong bishop = self.Bishop | self.Queen;
            while (bishop != 0)
            {
                int i = TrailingZeroCount(bishop);
                AddMoves(new Coords(i), BishopMove(1ul << i, empty) & ~anySelf);
                bishop &= bishop - 1;
            }

            ulong rook = self.Rook | self.Queen;
            while (rook != 0)
            {
                int i = TrailingZeroCount(rook);
                AddMoves(new Coords(i), RookMove(1ul << i, empty) & ~anySelf);
                rook &= rook - 1;
            }

            AddMoves(new Coords(TrailingZeroCount(self.King)), KingMove(self.King) & ~anySelf);

            int castleOffset = Player == Color.White ? 0 : 56; // Move up 7 ranks for black
            CastlingRight castling = Player == Color.White ? WhiteCastling : BlackCastling;
            ulong baseRank = (anySelf | anyOpponent) >> castleOffset;
            if (castling.HasKingSide() && (baseRank & 0x60) == 0)
                moves.Add(new Move(new Coords(4 + castleOffset), new Coords(6 + castleOffset), MoveType.ShortCastle));
            if (castling.HasQueenSide() && (baseRank & 0x0e) == 0)
                moves.Add(new Move(new Coords(4 + castleOffset), new Coords(2 + castleOffset), MoveType.LongCastle));

            return moves;

            void AddMoves(Coords src, ulong dst)
            {
                while (dst != 0)
                {
                    moves.Add(new Move(src, new Coords(TrailingZeroCount(dst))));
                    dst &= dst - 1;
                }
            }
        }

        private readonly List<Move> GenerateWhitePawnMoves(ulong pawn, ulong opponent, ulong empty)
        {
            const ulong notAFile = 0xfefefefefefefefeul;
            const ulong notHFile = 0x7f7f7f7f7f7f7f7ful;
            List<Move> moves = [];
            AddPawnMoves(moves, WhitePawnForward(pawn, empty), -8);
            AddPawnMoves(moves, WhitePawnTwoSquares(pawn, empty), -16, MoveType.PawnTwoSquares);
            AddPawnMoves(moves, ((pawn & notAFile) << 7) & opponent, -7); // Attack left
            AddPawnMoves(moves, ((pawn & notHFile) << 9) & opponent, -9); // Attack right
            AddPawnMoves(moves, ((pawn & notAFile) << 7) & EnPassantMask, -7, MoveType.EnPassant); // Attack left
            AddPawnMoves(moves, ((pawn & notHFile) << 9) & EnPassantMask, -9, MoveType.EnPassant); // Attack right
            return moves;
        }

        private readonly List<Move> GenerateBlackPawnMoves(ulong pawn, ulong opponent, ulong empty)
        {
            const ulong notAFile = 0xfefefefefefefefeul;
            const ulong notHFile = 0x7f7f7f7f7f7f7f7ful;
            List<Move> moves = [];
            AddPawnMoves(moves, BlackPawnForward(pawn, empty), 8);
            AddPawnMoves(moves, BlackPawnTwoSquares(pawn, empty), 16, MoveType.PawnTwoSquares);
            AddPawnMoves(moves, ((pawn & notAFile) >> 9) & opponent, 9); // Attack left
            AddPawnMoves(moves, ((pawn & notHFile) >> 7) & opponent, 7); // Attack right
            AddPawnMoves(moves, ((pawn & notAFile) >> 9) & EnPassantMask, 9, MoveType.EnPassant); // Attack left
            AddPawnMoves(moves, ((pawn & notHFile) >> 7) & EnPassantMask, 7, MoveType.EnPassant); // Attack right
            return moves;
        }

        private static void AddPawnMoves(
            List<Move> moves, ulong mask, int offset, MoveType type = MoveType.None)
        {
            while (mask != 0)
            {
                int i = TrailingZeroCount(mask);
                moves.Add(new Move(new Coords(i + offset), new Coords(i), type));
                mask &= mask - 1;
            }
        }

        private void ParseFenPieces(string pieces)
        {
            int idx = 0;
            for (int i = 7; i >= 0; i--)
            {
                int j = 0;
                while (true)
                {
                    if (idx >= pieces.Length)
                        throw new ArgumentException("Unfinished piece placements");
                    if (pieces[idx] is > '0' and <= '8')
                        j += pieces[idx] - '0';
                    else
                        Board[new Coords(i, j++)] = Piece.FromFen(pieces[idx]);
                    idx++;
                    if (j == 8) break;
                    if (j > 8) throw new ArgumentException("Too many spaces in a rank");
                }
                if (idx != pieces.Length && pieces[idx++] != '/')
                    throw new ArgumentException("Ranks should be separated by a slash");
            }
            if (idx != pieces.Length)
                throw new ArgumentException("Extraneous characters after piece placements");
        }

        private void ParseFenActivePlayer(string ch)
        {
            Player = ch switch
            {
                "w" => Color.White,
                "b" => Color.Black,
                _ => throw new ArgumentException($"Invalid active player indicator: {ch}")
            };
        }

        private void ParseFenCastlingRight(string fen)
        {
            if (fen == "-") return;
            foreach (char ch in fen)
                switch (ch)
                {
                    case 'K':
                        WhiteCastling = WhiteCastling.AddKingSide();
                        break;
                    case 'Q':
                        WhiteCastling = WhiteCastling.AddQueenSide();
                        break;
                    case 'k':
                        BlackCastling = BlackCastling.AddKingSide();
                        break;
                    case 'q':
                        BlackCastling = BlackCastling.AddQueenSide();
                        break;
                    default: throw new ArgumentException($"Unknown castling indicator {ch}");
                }
        }

        private static int ParseFenMoveCount(string count)
        {
            if (int.TryParse(count, out int res)) return res;
            throw new ArgumentException($"Invalid move count {count}");
        }
    }

    private enum CheckState { None, Check, Checkmate }

    private const RegexOptions RegOptions =
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private static readonly Regex NormalNotationRegex =
        new(@"^([NBRQK]?)([a-h]?)([1-8]?)(x?)([a-h]?)([1-8]?)(?:=?([NBRQ]?))([\+#]?)$", RegOptions);
    private static readonly Regex CastleRegex =
        new(@"^((?:0-0-0)|(?:O-O-O)|(?:0-0)|(?:O-O))([\+#]?)$", RegOptions);

    private State _state;
    private int _50MoveCounter;
    private Dictionary<(Board, Color), int> _reachedBoards = new();

    private Move? ParseCastling(string notation)
    {
        Match match = CastleRegex.Match(notation);
        if (!match.Success) return null;
        MoveType castleType = match.Groups[1].Length == 3 ? MoveType.ShortCastle : MoveType.LongCastle;
        CheckState check = ParseCheck(match.Groups[2].ValueSpan);
        Move move = _state.GetLegalMoves().Find(m => m.Type == castleType);
        if (move == new Move()) throw new MoveIllegalException();
        if (check != CheckState.None && check != VerifyMoveChecks(move))
            throw new ClarificationIncorrectException(move);
        return move;
    }

    private Move ParseNormalMove(string notation)
    {
        if (!notation.Any(c => c is >= 'a' and <= 'h')) // Must contain at least one file specifier
            throw new NotAMoveException();

        // Illegal, but for demonstration:
        // Ra7xa8=Q#: R, a, 7, x, a, 8, Q, #
        Match match = NormalNotationRegex.Match(notation);
        if (!match.Success) throw new NotAMoveException();

        Piece piece = new(_state.Player, ToPieceType(match.Groups[1].ValueSpan));
        int fromFile = ParseFile(match.Groups[2].ValueSpan), fromRank = ParseRank(match.Groups[3].ValueSpan);
        bool taking = !match.Groups[4].ValueSpan.IsEmpty;
        int toFile = ParseFile(match.Groups[5].ValueSpan), toRank = ParseRank(match.Groups[6].ValueSpan);
        PieceType promote = ToPieceType(match.Groups[7].ValueSpan);
        CheckState check = ParseCheck(match.Groups[8].ValueSpan);

        if (toFile == -1 && toRank == -1)
        {
            (toFile, fromFile) = (fromFile, toFile);
            (toRank, fromRank) = (fromRank, toRank);
        }

        List<Move> filteredMoves = _state.GetLegalMoves().Where(move =>
            _state.Board[move.Src] == piece &&
            (fromFile == -1 || move.Src.File == fromFile) &&
            (fromRank == -1 || move.Src.Rank == fromRank) &&
            (toFile == -1 || move.Dst.File == toFile) &&
            (toRank == -1 || move.Dst.Rank == toRank)).ToList();

        Move result = filteredMoves.Count switch
        {
            0 => throw new MoveIllegalException(),
            1 => filteredMoves[0],
            _ => throw new MoveAmbiguousException(filteredMoves)
        };

        bool shouldPromote =
            _state.Board[result.Src].Type == PieceType.Pawn &&
            result.Dst.Rank is 0 or 7;
        if (shouldPromote == (promote == PieceType.Pawn))
            throw new MoveIllegalException();
        if (promote != PieceType.Pawn) result = new Move(result.Src, result.Dst, (MoveType)promote);

        if (!MoveCaptures(result) && taking)
            throw new ClarificationIncorrectException(result);
        if (check != CheckState.None && check != VerifyMoveChecks(result))
            throw new ClarificationIncorrectException(result);

        return result;
    }

    private static PieceType ToPieceType(ReadOnlySpan<char> capture)
    {
        if (capture.IsEmpty) return PieceType.Pawn;
        return capture[0] switch
        {
            'N' => PieceType.Knight,
            'B' => PieceType.Bishop,
            'R' => PieceType.Rook,
            'Q' => PieceType.Queen,
            'K' => PieceType.King,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static char PieceCharacter(PieceType type) => type switch
    {
        PieceType.Knight => 'N',
        PieceType.Bishop => 'B',
        PieceType.Rook => 'R',
        PieceType.Queen => 'Q',
        PieceType.King => 'K',
        _ => 'P'
    };

    private static int ParseFile(ReadOnlySpan<char> capture) => capture.IsEmpty ? -1 : capture[0] - 'a';
    private static int ParseRank(ReadOnlySpan<char> capture) => capture.IsEmpty ? -1 : capture[0] - '1';

    private static CheckState ParseCheck(ReadOnlySpan<char> capture)
    {
        if (capture.IsEmpty) return CheckState.None;
        return capture[0] switch
        {
            '+' => CheckState.Check,
            '#' => CheckState.Checkmate,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private bool MoveCaptures(Move move) =>
        _state.Board[move.Dst].Type != PieceType.None || // Normal taking
        (_state.Board[move.Src].Type == PieceType.Pawn &&
         1ul << move.Dst.Index == _state.EnPassantMask); // En passant

    private CheckState VerifyMoveChecks(Move move)
    {
        State state = _state;
        state.PlayMoveUnchecked(move);
        if (state.IsUnderCheck())
            return state.GetLegalMoves().Count == 0 ? CheckState.Checkmate : CheckState.Check;
        return CheckState.None;
    }
}

public class MoveParsingException : Exception;

public class NotAMoveException : MoveParsingException;

public class MoveAmbiguousException(List<Move> moves) : MoveParsingException
{
    public List<Move> Moves { get; } = moves;
}

public class MoveIllegalException : MoveParsingException;

public class ClarificationIncorrectException(Move move) : MoveParsingException
{
    public Move Move { get; } = move;
}
