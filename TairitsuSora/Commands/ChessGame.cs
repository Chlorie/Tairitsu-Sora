using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.Chess;
using TairitsuSora.Core;
using TairitsuSora.TairitsuSora.Commands.GameCommand;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class ChessGame : TwoPlayerBoardGame, IDisposable
{
    public override CommandInfo Info => new()
    {
        Trigger = "c",
        Summary = "国际象棋",
        Description = "古老的桌上博弈。"
    };

    public ChessGame()
    {
        _client = new HttpClient();
        _drawerRes = new BoardDrawerResources(_client, "./data/chess");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _client.Dispose();
    }

    [MessageHandler(Description = $"发起对局请求。用代数记谱法输入棋步。{SubcommandDescription}")]
    public ValueTask StartGame(GroupMessageEventArgs ev) => StartGame(ev, DoGameProcedureAsync);

    protected override TwoPlayerBoardGameState CreateGameState(long group, long player1, long player2)
        => new GameState(group, player1, player2, _drawerRes);

    private class GameState(long group, long player1, long player2, BoardDrawerResources drawerRes)
        : TwoPlayerBoardGameState(group, player1, player2), IDisposable
    {
        public override string Player1Verb => "执白";
        public override string Player2Verb => "执黑";
        public override string Player1Noun => "白方";
        public override string Player2Noun => "黑方";
        public override string PleaseStartPrompt => "请白方开始。";
        public override bool Player1IsNext => _game.Player == Color.White;

        public override bool IsMoveReply(string text) => text.Length <= 7 && Game.MatchMoveSyntax(text);

        public override ValueTask<byte[]> GenerateBoardImage() => _drawer.DrawAsPng(_game,
            _lastMove is { Src: var src, Dst: var dst } ? [src, dst] : []);

        public override MoveResult PlayMove(string message)
        {
            try { _lastMove = _game.ParseMove(message); }
            catch (Exception ex) { return new Illegal(DescribeException(ex)); }
            _lastMoveNotation = _game.NotateMove(_lastMove.Value);
            var outcome = _game.PlayMove(_lastMove.Value);
            if (outcome != Game.Outcome.None) return new Terminal(DescribeOutcome(outcome));
            return new Ongoing();
        }

        public override string DescribeState()
        {
            string res = $"轮到{NextPlayerNoun}落子。";
            return _lastMoveNotation is null ? res
                : $"{(_game.HalfMoves + 1) / 2}{(_game.HalfMoves % 2 == 1 ? '.' : '…')} {_lastMoveNotation}\n{res}";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _drawer.Dispose();
        }

        private Game _game = Game.FromStartingPosition();
        private Move? _lastMove;
        private string? _lastMoveNotation;
        private BoardDrawer _drawer = new(drawerRes);

        private string DescribeException(Exception ex) =>
            ex switch
            {
                MoveAmbiguousException e =>
                    $"表示有歧义，有可能是：{string.Join('/', e.Moves.Select(_game.NotateMove))}",
                MoveIllegalException => "你不能下这一步棋",
                ClarificationIncorrectException e =>
                    $"附加信息与棋盘状态不对应，你是不是指 {_game.NotateMove(e.Move)}",
                _ => ex.Message
            };

        private string DescribeOutcome(Game.Outcome outcome) => outcome switch
        {
            Game.Outcome.DrawByRepetition => "和棋 (同一局面重复三次)",
            Game.Outcome.DrawBy50MoveRule => "和棋 (50 步内无人推兵或吃子)",
            Game.Outcome.DrawByStalemate => "和棋 (僵局)",
            Game.Outcome.WhiteWin => "白棋胜出",
            Game.Outcome.BlackWin => "黑棋胜出",
            _ => ""
        };
    }

    private HttpClient _client;
    private BoardDrawerResources _drawerRes;
}
