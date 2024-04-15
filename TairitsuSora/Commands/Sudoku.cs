using SkiaSharp;
using System.Globalization;
using Sora.Entities;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Sudoku : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "sd",
        Summary = "数独",
        Description =
            "经典纸笔谜题。\n" +
            "难度 [diff] 为 0 到 5 的整数（更高难度目前正在测试中，题目难度会有较大波动）\n" +
            "2023-09-14: 重置难度 5 题库并将其加入通常难度\n" +
            "2023-09-12: 重置所有题库，增加题目回顾功能\n" +
            "2023-09-09: 添加难度 0，重置难度 1 的题库"
    };

    public override async ValueTask InitializeAsync()
    {
        _puzzles = new Puzzle[DifficultyNames.Length][];
        for (int i = 0; i < DifficultyNames.Length; i++)
            _puzzles[i] = (await File.ReadAllLinesAsync($"data/sudoku-puzzles/{DifficultyFileNames[i]}.txt"))
                .Select(Puzzle.FromString).ToArray();
    }

    [MessageHandler(Signature = "lvl", Description = "难度分级依据说明")]
    public string ShowLevelingReasons() => LevelingReasons;

    [MessageHandler(Signature = "dp $diff $date", Description = "展示每日数独题目，指定 [date] 以查看过往题目")]
    public MessageBody ShowPuzzle(int diff, string? date)
    {
        if (diff < 0 || diff >= DifficultyFileNames.Length) return "难度需要是一个 0 到 5 的整数";
        string additional = diff == 6 ? $"{DifficultyNames[diff]}难度 ({diff}) 正在测试中，后期题目可能会有变动\n" : "";
        if (ParseDate(date) is not { } day) return $"无法将 {date} 分析为日期";
        if (day > DateTime.Today) return "不可以偷看后面的题目哦";
        if (day < FirstDay) return $"日期最早是 {FirstDay:yyyy-MM-dd}";

        int days = (day - FirstDay).Days;
        Puzzle puzzle = _puzzles[diff][days];
        using BoardDrawer drawer = new(puzzle.Given);
        byte[] image = drawer.DrawAsPng();
        return new MessageBody()
            .Text($"每日数独 {day:yyyy-MM-dd} / 难度: {DifficultyNames[diff]} ({diff})\n{additional}")
            .Image(image)
            .Text($"复制题目: {puzzle.Given}");
    }

    [MessageHandler(Signature = "dp ans $diff $date", Description = "查看过往题目答案，[date] 为日期")]
    public MessageBody ShowAnswer(int diff, string? date)
    {
        if (diff < 0 || diff >= DifficultyFileNames.Length) return "难度需要是一个 0 到 5 的整数";
        if (ParseDate(date) is not { } day) return $"无法将 {date} 分析为日期";
        if ((DateTime.Today - day).Days < 1) return "题目公布一天后可查看答案";
        if (day < FirstDay) return $"日期最早是 {FirstDay:yyyy-MM-dd}";

        int days = (day - FirstDay).Days;
        Puzzle puzzle = _puzzles[diff][days];
        using BoardDrawer drawer = new(puzzle.Given, puzzle.Solution);
        byte[] image = drawer.DrawAsPng();
        return new MessageBody()
            .Text($"数独答案 {day:yyyy-MM-dd} / 难度: {DifficultyNames[diff]} ({diff})\n")
            .Image(image)
            .Text($"复制题目: {puzzle.Given}");
    }

    private static readonly string[] DifficultyFileNames =
        ["trivial", "casual", "beginner", "intermediate", "advanced", "expert", "master"];
    private static readonly string[] DifficultyNames = ["显然", "休闲", "入门", "进阶", "高级", "专家", "大师"];
    private static readonly DateTime FirstDay = new(2023, 8, 31);

    private static readonly string LevelingReasons = $"""
        难度分级依据:
        题目难度按照解题时需要采用的最难技巧确定，技巧体感难度可能存在个人差。
        {DifficultyNames[0]} (0): 只需要区内唯一余数、宫内排除即可解出题目；
        {DifficultyNames[1]} (1): 唯一候选、唯一位置、区域相交；
        {DifficultyNames[2]} (2): 显/隐数对、X翼；
        {DifficultyNames[3]} (3): 显/隐三元组、带鳍X翼、XY翼、XYZ翼、W翼、（带鳍）剑鱼、多宝鱼（3长度X链）、3长度XY链；
        {DifficultyNames[4]} (4): 显/隐四元组、远程数对、简单染色法、（带鳍）水母、最大5长度X链、最大5长度XY链；
        {DifficultyNames[5]} (5): 交替推理链、连续循环、双区不交子集（SdC）、待定数组双强链（ALS-XZ）；
        {DifficultyNames[6]} (6): 存在至少一个步骤无法用以上列出的任何技巧解出。
        """;

    private Puzzle[][] _puzzles = null!;

    private static DateTime? ParseDate(string? date)
    {
        if (date is null) return DateTime.Today;
        string[] formats = ["yyyy-M-d", "yyyy/M/d", "yy-M-d", "yy/M/d", "M-d", "M/d"];
        return DateTime.TryParseExact(date, formats, null, DateTimeStyles.None, out DateTime res) ? res : null;
    }

    private record struct Puzzle(Board Given, Board Solution)
    {
        public static Puzzle FromString(string line)
        {
            string[] parts = line.Split(' ', 2);
            return new Puzzle(Board.FromString(parts[0]), Board.FromString(parts[1]));
        }
    }

    private class Board
    {
        public int? this[int i, int j]
        {
            get => _cells[i, j];
            set => _cells[i, j] = value;
        }

        public static Board FromString(ReadOnlySpan<char> str)
        {
            if (str.Length != 81) throw new ArgumentException("The string must be of length 81");
            Board board = new();
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 9; j++)
                {
                    char ch = str[i * 9 + j];
                    if (ch == '.') board._cells[i, j] = null;
                    else board._cells[i, j] = ch - '1';
                }
            return board;
        }

        public override string ToString()
        {
            char[] chars = new char[81];
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 9; j++)
                    chars[i * 9 + j] = _cells[i, j] is { } n ? (char)(n + '1') : '.';
            return new string(chars);
        }

        private int?[,] _cells = new int?[9, 9];
    }

    private class BoardDrawer : IDisposable
    {
        public BoardDrawer(Board board, Board? solution = null)
        {
            _board = board;
            _solution = solution;
            _bitmap = new SKBitmap(new SKImageInfo(ImageSize, ImageSize, SKColorType.Rgba8888));
            _canvas = new SKCanvas(_bitmap);
            _canvas.Clear(Black);
            _paint.TextAlign = SKTextAlign.Center;
        }

        public byte[] DrawAsPng()
        {
            DrawDividers();
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 9; j++)
                    DrawGiven(i, j);

            using MemoryStream ms = new();
            _bitmap.Encode(ms, SKEncodedImageFormat.Png, quality: 100);
            return ms.ToArray();
        }

        public void Dispose()
        {
            _canvas.Dispose();
            _bitmap.Dispose();
        }

        private const int CellSize = 60;
        private const int BoxDividerWidth = 5;
        private const int CellDividerWidth = 2;
        private const int FontSize = 40;
        private const int FontBaseline = 46;
        private const int ImageSize = 9 * CellSize + 4 * BoxDividerWidth + 6 * CellDividerWidth;
        private static readonly SKTypeface TypeFace = SKTypeface.FromFile("data/Roboto-Medium.ttf");
        private static readonly SKFont Font = new(TypeFace, FontSize);
        private static readonly SKColor White = new(235, 235, 235);
        private static readonly SKColor Blue = new(130, 160, 220);
        private static readonly SKColor Black = new(21, 21, 21);
        private Board _board;
        private Board? _solution;
        private SKPaint _paint = new();
        private SKBitmap _bitmap;
        private SKCanvas _canvas;

        private void DrawDividers()
        {
            _paint.Color = White;
            const int bandSize = BoxDividerWidth + 2 * CellDividerWidth + 3 * CellSize;
            for (int i = 0; i <= 3; i++)
            {
                int boxDividerStart = bandSize * i, boxDividerEnd = boxDividerStart + BoxDividerWidth;
                SKRect verticalBoxDivider = new(boxDividerStart, 0, boxDividerEnd, ImageSize);
                _canvas.DrawRect(verticalBoxDivider, _paint);
                SKRect horizontalBoxDivider = new(0, boxDividerStart, ImageSize, boxDividerEnd);
                _canvas.DrawRect(horizontalBoxDivider, _paint);
                if (i == 3) return;
                int offset = boxDividerEnd - CellDividerWidth;
                for (int j = 1; j < 3; j++)
                {
                    int cellDividerStart = offset + j * (CellDividerWidth + CellSize),
                        cellDividerEnd = cellDividerStart + CellDividerWidth;
                    SKRect vertical = new(cellDividerStart, 0, cellDividerEnd, ImageSize);
                    _canvas.DrawRect(vertical, _paint);
                    SKRect horizontal = new(0, cellDividerStart, ImageSize, cellDividerEnd);
                    _canvas.DrawRect(horizontal, _paint);
                }
            }
        }

        private void DrawGiven(int i, int j)
        {
            if (_board[i, j] is null && _solution is null) return;
            _paint.Color = _board[i, j].HasValue ? White : Blue;
            string text = ((_board[i, j] ?? _solution![i, j] ?? 0) + 1).ToString();
            int y = BoxDividerWidth + (i / 3) * (BoxDividerWidth - CellDividerWidth) + i * CellDividerWidth + i * CellSize + FontBaseline;
            int x = BoxDividerWidth + (j / 3) * (BoxDividerWidth - CellDividerWidth) + j * CellDividerWidth + j * CellSize + CellSize / 2;
            _canvas.DrawText(text, x, y, Font, _paint);
        }
    }
}
