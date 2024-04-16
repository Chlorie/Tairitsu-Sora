using SkiaSharp;

namespace TairitsuSora.Commands.UltimateTicTacToe;

public class BoardDrawer : IDisposable
{
    public BoardDrawer()
    {
        _bitmap = new SKBitmap(new SKImageInfo(9 * CellSize, 9 * CellSize, SKColorType.Rgba8888));
        _canvas = new SKCanvas(_bitmap);
    }

    public byte[] DrawBoard(Board board, bool highlightPlayableSection = false)
    {
        DrawEmptyBoard();
        if (highlightPlayableSection) DrawPlayableSectionHighlight(board);
        DrawPieces(board);
        DrawCoords();
        using MemoryStream ms = new();
        _bitmap.Encode(ms, SKEncodedImageFormat.Png, quality: 100);
        return ms.ToArray();
    }

    public void Dispose()
    {
        _paint.Dispose();
        _canvas.Dispose();
        _bitmap.Dispose();
    }

    private const int CellSize = 60;
    private const int FontSize = 12;
    private const int LineThickness = 2;
    private const int SmallThickness = 6;
    private const int LargeThickness = 12;
    private const float SymbolRatio = 0.65f;
    private const float SmallRadius = (CellSize - 2) * SymbolRatio / 2;
    private const float LargeRadius = (3 * CellSize - 2) * SymbolRatio / 2;
    private static readonly SKPoint RankOffset = new(5, 15);
    private static readonly SKPoint FileOffset = new(-5, -5);

    private static readonly SKColor White = new(235, 235, 235);
    private static readonly SKColor Red = new(212, 85, 85);
    private static readonly SKColor Green = new(85, 212, 85);
    private static readonly SKColor Blue = new(85, 85, 212);
    private static readonly SKTypeface TypeFace = SKTypeface.FromFile("data/NotoSans.otf");
    private static readonly SKFont CoordsFont = new(TypeFace, FontSize);
    private const byte DeterminedSectionFillAlpha = 166;
    private const byte PlayableSectionFillAlpha = 63;

    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private SKPaint _paint = new(CoordsFont) { IsAntialias = true, TextAlign = SKTextAlign.Center };

    private void DrawEmptyBoard()
    {
        _canvas.Clear(SKColors.Black);
        _paint.StrokeCap = SKStrokeCap.Square;
        _paint.StrokeWidth = LineThickness;
        for (int i = 1; i < 9; i++)
        {
            _paint.Color = White.WithAlpha(i % 3 == 0 ? (byte)255 : (byte)85);
            _canvas.DrawLine(0, i * CellSize, 9 * CellSize, i * CellSize, _paint);
            _canvas.DrawLine(i * CellSize, 0, i * CellSize, 9 * CellSize, _paint);
        }
    }

    private void DrawPlayableSectionHighlight(Board board)
    {
        SKColor color = (board.ActivePlayer == Board.CellType.Circle ? Red : Blue)
            .WithAlpha(PlayableSectionFillAlpha);
        if (board.PlayableSection is var (rank, file))
            FillSection(rank, file, color);
        else
        {
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (board.AtLargeBoard(i, j) == Board.CellType.None)
                        FillSection(i, j, color);
        }
    }

    private void DrawPieces(Board board)
    {
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                switch (board[i, j])
                {
                    case Board.CellType.None: break;
                    case Board.CellType.Circle: DrawCircle(i, j); break;
                    case Board.CellType.Cross: DrawCross(i, j); break;
                    case Board.CellType.Tie: default: throw new ArgumentOutOfRangeException();
                }
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                var cell = board.AtLargeBoard(i, j);
                if (cell == Board.CellType.None) continue;
                FillSection(i, j, SKColors.Black.WithAlpha(DeterminedSectionFillAlpha));
                switch (cell)
                {
                    case Board.CellType.Circle: DrawCircle(3 * i, 3 * j, true); break;
                    case Board.CellType.Cross: DrawCross(3 * i, 3 * j, true); break;
                    case Board.CellType.Tie: DrawTie(3 * i, 3 * j, true); break;
                    case Board.CellType.None: default: throw new ArgumentOutOfRangeException();
                }
            }
    }

    private (float x, float y) GetCenter(int rank, int file, bool large) =>
        ((file + (large ? 3 : 1) / 2f) * CellSize, (rank + (large ? 3 : 1) / 2f) * CellSize);

    private void DrawCircle(int rank, int file, bool large = false)
    {
        _paint.Color = Red;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = large ? LargeThickness : SmallThickness;
        float radius = large ? LargeRadius : SmallRadius;
        (float x, float y) = GetCenter(rank, file, large);
        _canvas.DrawCircle(x, y, radius, _paint);
    }

    private void DrawCross(int rank, int file, bool large = false)
    {
        _paint.Color = Blue;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeCap = SKStrokeCap.Butt;
        _paint.StrokeWidth = large ? LargeThickness : SmallThickness;
        float radius = large ? LargeRadius : SmallRadius;
        (float x, float y) = GetCenter(rank, file, large);
        _canvas.DrawLine(x - radius, y - radius, x + radius, y + radius, _paint);
        _canvas.DrawLine(x - radius, y + radius, x + radius, y - radius, _paint);
    }

    private void DrawTie(int rank, int file, bool large = false)
    {
        _paint.Color = Green;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeCap = SKStrokeCap.Butt;
        _paint.StrokeWidth = large ? LargeThickness : SmallThickness;
        float radius = large ? LargeRadius : SmallRadius;
        (float x, float y) = GetCenter(rank, file, large);
        _canvas.DrawLine(x - radius, y, x + radius, y, _paint);
    }

    private void DrawCoords()
    {
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = White;
        for (int i = 0; i < 9; i++)
        {
            string rank = (i + 1).ToString();
            _paint.TextAlign = SKTextAlign.Left;
            _canvas.DrawText(rank, new SKPoint(0, i * CellSize) + RankOffset, _paint);

            string file = ((char)(i + 'a')).ToString();
            _paint.TextAlign = SKTextAlign.Right;
            _canvas.DrawText(file, new SKPoint((i + 1) * CellSize, 9 * CellSize) + FileOffset, _paint);
        }
    }

    private void FillSection(int rank, int file, SKColor color)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Fill;
        _canvas.DrawRect(3 * file * CellSize + LineThickness / 2, 3 * rank * CellSize + LineThickness / 2,
            3 * CellSize - LineThickness, 3 * CellSize - LineThickness, _paint);
    }
}
