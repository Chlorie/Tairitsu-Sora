using SkiaSharp;

namespace TairitsuSora.Commands.Othello;

public class BoardDrawer : IDisposable
{
    public BoardDrawer()
    {
        _bitmap = new SKBitmap(new SKImageInfo(8 * CellSize, 8 * CellSize, SKColorType.Rgba8888));
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
    private const float DiskRadius = 20;
    private const float LegalMoveIndicatorRadius = 5;
    private const float PositioningPointRadius = 5;
    private static readonly SKPoint RankOffset = new(5, -5);
    private static readonly SKPoint FileOffset = new(-5, 15);

    private static readonly SKColor White = new(235, 235, 235);
    private static readonly SKColor Black = new(0, 0, 0);
    private static readonly SKColor Green = new(80, 160, 120);
    private static readonly SKTypeface TypeFace = SKTypeface.FromFile("data/NotoSans.otf");
    private static readonly SKFont CoordsFont = new(TypeFace, FontSize);
    private const byte LegalMoveIndicatorAlpha = 127;

    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private SKPaint _paint = new(CoordsFont) { IsAntialias = true, TextAlign = SKTextAlign.Center };

    private void DrawEmptyBoard()
    {
        _canvas.Clear(Green);
        _paint.StrokeCap = SKStrokeCap.Square;
        _paint.Style = SKPaintStyle.Fill;
        _paint.StrokeWidth = LineThickness;
        _paint.Color = Black.WithAlpha(127);
        for (int i = 1; i < 8; i++)
        {
            _canvas.DrawLine(0, i * CellSize, 8 * CellSize, i * CellSize, _paint);
            _canvas.DrawLine(i * CellSize, 0, i * CellSize, 8 * CellSize, _paint);
        }
        _canvas.DrawCircle(2 * CellSize, 2 * CellSize, PositioningPointRadius, _paint);
        _canvas.DrawCircle(2 * CellSize, 6 * CellSize, PositioningPointRadius, _paint);
        _canvas.DrawCircle(6 * CellSize, 2 * CellSize, PositioningPointRadius, _paint);
        _canvas.DrawCircle(6 * CellSize, 6 * CellSize, PositioningPointRadius, _paint);
    }

    private void DrawPlayableSectionHighlight(Board board)
    {
        SKColor color = (board.ActivePlayer == Board.CellType.Black ? Black : White)
            .WithAlpha(LegalMoveIndicatorAlpha);
        for (int i = 0; i < 64; i++)
            if ((board.LegalMoves & (1ul << i)) != 0)
                DrawDisk(i / 8, i % 8, color, LegalMoveIndicatorRadius);
    }

    private void DrawPieces(Board board)
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                if (board[i, j] != Board.CellType.None)
                    DrawDisk(i, j, GetPlayerColor(board[i, j]), DiskRadius);
    }

    private static SKColor GetPlayerColor(Board.CellType player) =>
        player is Board.CellType.Black ? Black : White;

    private (float x, float y) GetCenter(int rank, int file) =>
        ((file + 0.5f) * CellSize, (rank + 0.5f) * CellSize);

    private void DrawDisk(int rank, int file, SKColor color, float radius)
    {
        _paint.Color = color;
        _paint.Style = SKPaintStyle.Fill;
        (float x, float y) = GetCenter(rank, file);
        _canvas.DrawCircle(x, y, radius, _paint);
    }

    private void DrawCoords()
    {
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = Black;
        for (int i = 0; i < 8; i++)
        {
            string rank = (i + 1).ToString();
            _paint.TextAlign = SKTextAlign.Left;
            _canvas.DrawText(rank, new SKPoint(0, (i + 1) * CellSize) + RankOffset, _paint);

            string file = ((char)(i + 'A')).ToString();
            _paint.TextAlign = SKTextAlign.Right;
            _canvas.DrawText(file, new SKPoint((i + 1) * CellSize, 0) + FileOffset, _paint);
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
