using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.UltimateTicTacToe;
using TairitsuSora.Core;
using TairitsuSora.TairitsuSora.Commands.GameCommand;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class UltimateTicTacToeGame : TwoPlayerBoardGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "uttt",
        DisplayName = "Ultimate Tic-Tac-Toe",
        Summary = "套娃井字棋",
        Description = "经典井字棋游戏，但是是两层的。"
    };

    [MessageHandler(Description = $"发起对局请求。输入坐标来下棋，{SubcommandDescription}")]
    public ValueTask StartGame(GroupMessageEventArgs ev) => StartGame(ev, GameProcedureFactory(CreateGameState));

    private TwoPlayerBoardGameState CreateGameState(long group, long player1, long player2)
        => new GameState(group, player1, player2);

    private class GameState(long group, long player1, long player2)
        : TwoPlayerBoardGameState(group, player1, player2), IDisposable
    {
        public override string Player1Verb => "画叉";
        public override string Player2Verb => "画圈";
        public override string Player1Noun => "画叉一方";
        public override string Player2Noun => "画圈一方";
        public override string PleaseStartPrompt => "请画叉一方开始。";
        public override bool Player1IsNext => _board.ActivePlayer == Board.CellType.Cross;

        public override bool IsMoveReply(string text) => text is [>= 'a' and <= 'i', >= '1' and <= '9'];

        public override ValueTask<byte[]> GenerateBoardImage() => ValueTask.FromResult(_drawer.DrawBoard(_board, true));
        public override ValueTask<MoveResult> PlayMove(string message) => ValueTask.FromResult(PlayMoveImpl(message));

        public override string DescribeState()
        {
            string playableSectionDesc = _board.PlayableSection switch
            {
                null => "全盘",
                (1, 1) => "正中",
                { File: var file, Rank: var rank } =>
                    file switch { 0 => "左", 1 => "中", 2 => "右", _ => "" } +
                    rank switch { 0 => "上", 1 => "中", 2 => "下", _ => "" }
            };
            return $"轮到{NextPlayerNoun}了，可落子区域：{playableSectionDesc}。";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _drawer.Dispose();
        }

        private Board _board = new();
        private BoardDrawer _drawer = new();

        private MoveResult PlayMoveImpl(string message)
        {
            Board.Coords coords = Board.Coords.FromString(message);
            if (!_board.PlayableAt(coords))
                return new Illegal("目前不可在此处落子");
            _board.PlayAt(coords);
            if (_board.Result != Board.CellType.None)
                return new Terminal(_board.Result switch
                {
                    Board.CellType.Circle => "画圈一方胜出",
                    Board.CellType.Cross => "画叉一方胜出",
                    Board.CellType.Tie => "双方打成平局",
                    _ => throw new ArgumentOutOfRangeException()
                });
            return new Ongoing();
        }
    }
}
