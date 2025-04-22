using System.Text;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.Concyclic;
using TairitsuSora.Core;
using TairitsuSora.TairitsuSora.Commands.GameCommand;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class ConcyclicGame : TwoPlayerBoardGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "cc",
        DisplayName = "Concyclic",
        Summary = "四点共圆",
        Description = "圆幂定理？托勒密定理？高斯整数？这是什么神人游戏啊饶了我吧！"
    };

    [MessageHandler(Description =
        $"""
         发起对局请求。
         输入两位数作为坐标来画点，如 “57” 表示在格点 (5, 7) 画点。
         目标是避免自己画的点与任意另外三个已有的点形成四点共圆。注意，四点共线也认为是（半径无穷大的）共圆。
         输入 c 后接六位数字宣告对手上一步造成了四点共圆，如 “c012345” 表示宣告对手上一步画的点与 (0, 1) (2, 3) (4, 5) 这三个点形成了四点共圆。
         若这四点确实在同一个圆上，则你获胜，否则对手获胜。
         若某一步造成了四点共圆但对手没有发现直接画了下一个点，比赛继续。
         每人每步限时 2 分钟。
         {SubcommandDescription}
         """)]
    public ValueTask StartGame(GroupMessageEventArgs ev) => StartGame(ev, GameProcedureFactory(CreateGameState));

    private TwoPlayerBoardGameState CreateGameState(long group, long player1, long player2)
        => new GameState(group, player1, player2);

    private class GameState(long group, long player1, long player2)
        : TwoPlayerBoardGameState(group, player1, player2), IDisposable
    {
        public override string Player1Verb => "为先手";
        public override string Player2Verb => "为后手";
        public override string Player1Noun => "先手";
        public override string Player2Noun => "后手";
        public override string PleaseStartPrompt => "请先手画出第一个点。";
        public override bool Player1IsNext => _player1IsNext;
        public override MoveTimeLimit? TimeLimit { get; } = new(120, [30, 10]);

        public override bool IsMoveReply(string text)
        {
            static bool InRangeDigit(char c) => c is >= '0' and <= '8';
            return text.Length switch
            {
                2 => text.All(InRangeDigit),
                7 => text[0] == 'c' && text[1..].All(InRangeDigit),
                _ => false
            };
        }

        public override ValueTask<byte[]> GenerateBoardImage() =>
            ValueTask.FromResult(_drawer.DrawBoard(_board, _circles, _specified, _prevCircles ?? []));
        public override ValueTask<MoveResult> PlayMove(string message) => ValueTask.FromResult(PlayMoveImpl(message));

        public override string DescribeState()
        {
            StringBuilder res = new();
            if (_board.LastPlayed is { } point)
                res.Append($"上一位选手在 ({point.X}, {point.Y}) 处画了一个点。");
            return res.Append($"轮到{NextPlayerNoun}了。").ToString();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _drawer.Dispose();
        }

        private record struct LosingMove(int MoveIndex, Point Point, CircleInfo Circle, int TotalCircles);

        private Board _board = new();
        private BoardDrawer _drawer = new();
        private bool _player1IsNext = true;
        private List<CircleInfo>? _circles;
        private List<LosingMove> _losingMoves = [];
        private IEnumerable<CircleInfo>? _prevCircles;
        private IGeneralizedCircle? _specified;

        private MoveResult PlayMoveImpl(string msg)
        {
            if (msg.StartsWith('c'))
                return CheckImpl(msg);
            Point point = ParsePoint(msg);
            if (_board[point])
                return new Illegal("这个位置之前已经画过点了。");
            _board.Place(point);
            _player1IsNext = !_player1IsNext;
            if (_board.Points.Count == 81)
                return new Terminal("你们到底有没有在好好看啊，这局算你们俩都输了（恼）");
            var circles = _board.FindConcyclicQuadruples();
            if (circles.Count != 0)
                _losingMoves.Add(new LosingMove(_board.Points.Count, point, circles[0], circles.Count));
            return new Ongoing();
        }

        private MoveResult CheckImpl(string msg)
        {
            if (_board.Points.Count < 4)
                return new Illegal("棋盘上一共都没有四个点，你在干什么.jpg");
            Point[] points = [ParsePoint(msg[1..3]), ParsePoint(msg[3..5]), ParsePoint(msg[5..7])];
            Point last = _board.LastPlayed!.Value;
            if (points.Contains(last))
                return new Illegal($"{last} 是对手上一步画的点，请指出其他三个与之共圆的点。");
            if (points.Any(p => !_board[p]))
                return new Illegal("指定的点中有盘面上没有的点。");
            if (points.Distinct().Count() != 3)
                return new Illegal("指定的三个点有重复。");

            var circles = _board.FindConcyclicQuadruples();
            if (circles.Count != 0)
                _losingMoves.RemoveAt(_losingMoves.Count - 1);
            string prevCirclesDesc = DescribePreviousCircles();
            _prevCircles = _losingMoves.Select(m => m.Circle);
            if (circles.Count == 0)
                return new Terminal(prevCirclesDesc + "对手的上一步并没有造成四点共圆，因此对手获胜");

            var specified = GeneralizedCircle.FromPoints(points[0], points[1], points[2]);
            bool isCorrect = specified.Contains(last);
            if (isCorrect)
                _specified = specified;

            StringBuilder sb = new(prevCirclesDesc + "对手的上一步造成了四点共圆：");
            for (int i = 0; i < int.Min(3, circles.Count); i++)
                sb.Append("\n  ").Append(DescribeCircle(circles[i]));
            if (circles.Count > 3) sb.Append("\n  ...");
            _circles = circles;
            sb.Append(isCorrect ? "\n你胜出了" : "\n但对手的上一步并没有落在你指出的三点构成的圆上，因此对手获胜");
            return new Terminal(sb.ToString());
        }

        private string DescribeCircle(CircleInfo circle)
        {
            StringBuilder sb = new();
            sb.Append($"{circle.Circle.Equation()}: ");
            sb.AppendJoin(", ", circle.Points);
            return sb.ToString();
        }

        private string DescribePreviousCircles()
        {
            if (_losingMoves.Count == 0) return "";
            StringBuilder sb = new("在这一步之前就已经出现了四点共圆：");
            for (int i = 0; i < int.Min(3, _losingMoves.Count); i++)
            {
                LosingMove move = _losingMoves[i];
                string playerNoun = move.MoveIndex % 2 == 1 ? Player1Noun : Player2Noun;
                sb.Append($"\n  第 {move.MoveIndex} 步（{playerNoun}）@{move.Point}, " +
                          $"{DescribeCircle(_losingMoves[i].Circle)}");
                if (move.TotalCircles > 1)
                    sb.Append($" 以及其余 {move.TotalCircles - 1} 个圆");
            }
            if (_losingMoves.Count > 3) sb.Append("\n  ...");
            sb.Append('\n');
            return sb.ToString();
        }

        private static Point ParsePoint(string str) => new(str[0] - '0', str[1] - '0');
    }
}
