using SkiaSharp;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.Concyclic;

public class BoardDrawer : IDisposable
{
    public BoardDrawer()
    {
        const int imageSize = (GridSize + 1) * CellSize;
        _bitmap = new SKBitmap(new SKImageInfo(imageSize, imageSize, SKColorType.Rgba8888));
        _canvas = new SKCanvas(_bitmap);
    }

    public byte[] DrawBoard(
        Board board,
        List<CircleInfo>? circles,
        IGeneralizedCircle? specified,
        IEnumerable<CircleInfo> previousCircles)
    {
        DrawEmptyBoard();
        DrawCoords();
        circles ??= [];

        HashSet<IGeneralizedCircle> currentCircles = [.. circles.Select(c => c.Circle)];
        HashSet<Point> onCirclePoints = [];
        foreach (var c in circles)
            onCirclePoints.UnionWith(c.Points);

        foreach (var c in previousCircles)
            if (!currentCircles.Contains(c.Circle))
                DrawCircle(c.Circle, CircleType.Previous);
        foreach (var c in currentCircles)
            DrawCircle(c, c.Equals(specified) ? CircleType.Specified : CircleType.Current);

        foreach (Point p in board.Points)
            DrawPiece(p,
                p == board.LastPlayed ?
                    Green :
                onCirclePoints.Contains(p) ?
                    Blue :
                    Red);
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

    private enum CircleType
    {
        Specified,
        Current,
        Previous
    }

    private const int GridSize = 8;
    private const int CellSize = 60;
    private const int FontSize = 15;
    private const int LineThickness = 2;
    private const int CircleThickness = 3;
    private const float PieceRadius = 12;
    private const float CircleCenterRadius = 1.5f;
    private const float GridOffset = CellSize / 2;
    private const float MaskExtra = 0.25f;
    private const float XOffset = 8;
    private const float YOffset = -14;
    private static readonly float[] DashLengths = [6, 4];

    private static readonly SKColor White = new(235, 235, 235);
    private static readonly SKColor Red = new(225, 100, 100);
    private static readonly SKColor Green = new(85, 212, 85);
    private static readonly SKColor Grey = new(100, 100, 100);
    private static readonly SKColor Blue = new(90, 140, 240);
    private static readonly SKColor Orange = new(225, 140, 85);
    private static readonly SKTypeface TypeFace = SKTypeface.FromFile("data/NotoSans.otf");
    private static readonly SKFont CoordsFont = new(TypeFace, FontSize);

    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private SKPaint _paint = new() { IsAntialias = true };

    private void DrawEmptyBoard()
    {
        // Grid
        _canvas.Clear(SKColors.Black);
        _paint.StrokeCap = SKStrokeCap.Square;
        _paint.StrokeWidth = LineThickness;
        _paint.Color = White.WithAlpha(127);
        const float min = GridOffset;
        const float max = GridOffset + GridSize * CellSize;
        for (int i = 1; i <= GridSize; i++)
        {
            float x = i * CellSize + min, y = (GridSize - i) * CellSize + min;
            _canvas.DrawLine(min, y, max, y, _paint);
            _canvas.DrawLine(x, min, x, max, _paint);
        }

        // Axes
        _paint.StrokeCap = SKStrokeCap.Butt;
        _paint.Color = White.WithAlpha(200);
        const int extra = CellSize / 5;
        _canvas.DrawLine(min, min - extra, min, max, _paint);
        _canvas.DrawLine(min, max, max + extra, max, _paint);

        // Arrows
        _paint.Style = SKPaintStyle.Fill;
        const int arrowOffset = LineThickness * 3;
        const int arrowSize = LineThickness * 7;
        using SKPath xArrowhead = new();
        xArrowhead.MoveTo(max + extra + arrowSize, max);
        xArrowhead.LineTo(max + extra, max - arrowOffset);
        xArrowhead.LineTo(max + extra, max + arrowOffset);
        xArrowhead.Close();
        _canvas.DrawPath(xArrowhead, _paint);
        using SKPath yArrowhead = new();
        yArrowhead.MoveTo(min, min - extra - arrowSize);
        yArrowhead.LineTo(min - arrowOffset, min - extra);
        yArrowhead.LineTo(min + arrowOffset, min - extra);
        yArrowhead.Close();
        _canvas.DrawPath(yArrowhead, _paint);
    }

    private void DrawCoords()
    {
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = White;
        float verticalOffset = (CoordsFont.Metrics.Ascent + CoordsFont.Metrics.Descent) / 2;
        for (int i = 0; i <= GridSize; i++)
        {
            string s = i.ToString();
            _canvas.DrawText(s,
                new SKPoint(GridOffset + YOffset, GridOffset + (GridSize - i) * CellSize - verticalOffset),
                SKTextAlign.Right, CoordsFont, _paint);
            if (i == 0) continue;
            _canvas.DrawText(s,
                new SKPoint(GridOffset + i * CellSize, GridOffset + GridSize * CellSize + FontSize + XOffset),
                SKTextAlign.Center, CoordsFont, _paint);
        }
    }

    private SKPoint DrawnPosition(Point point) =>
        new(GridOffset + (float)point.X * CellSize, GridOffset + (GridSize - (float)point.Y) * CellSize);

    private void DrawPiece(Point point, SKColor color)
    {
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = color;
        _canvas.DrawCircle(DrawnPosition(point), PieceRadius, _paint);
    }

    private void DrawCircle(IGeneralizedCircle shape, CircleType type)
    {
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = CircleThickness;
        using SKPath mask = new();
        const float rectSize = GridSize * CellSize;
        const float extra = MaskExtra * CellSize;
        const float maskOffset = GridOffset - extra;
        const float maskSize = rectSize + 2 * extra;
        mask.AddRect(SKRect.Create(maskOffset, maskOffset, maskSize, maskSize));
        SKColor circleColor = type switch
        {
            CircleType.Specified => Orange.WithAlpha(200),
            CircleType.Current => Green.WithAlpha(200),
            CircleType.Previous => Grey.WithAlpha(200),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        SKPathEffect? pathEffect = type == CircleType.Previous
            ? SKPathEffect.CreateDash(DashLengths, 0) : null;
        using MaybeDisposable guard = new(pathEffect);
        _canvas.Save();
        _canvas.ClipPath(mask);
        switch (shape)
        {
            case Circle circle:
            {
                _paint.Color = White;
                var center = DrawnPosition(circle.Center);
                _canvas.DrawCircle(center, CircleCenterRadius, _paint);
                var radius = (float)Math.Sqrt((double)circle.SqrRadius) * CellSize;
                _paint.Color = circleColor;
                _paint.PathEffect = pathEffect;
                _canvas.DrawCircle(center, radius, _paint);
                _paint.PathEffect = null;
                break;
            }
            case Line line:
            {
                var (p, q) = FindLineEnds(line);
                _canvas.DrawLine(DrawnPosition(p), DrawnPosition(q), _paint);
                break;
            }
        }
        _paint.PathEffect = null;
        _canvas.Restore();
    }

    private static (Point, Point) FindLineEnds(Line line)
    {
        const int min = -1, max = GridSize + 1;
        if (line.YCoef == 0)
        {
            Rational x = -line.Constant / line.XCoef;
            return (new Point(x, min), new Point(x, max));
        }
        Rational yMin = (line.Constant - line.XCoef * min) / line.YCoef;
        Rational yMax = (line.Constant - line.XCoef * max) / line.YCoef;
        return (new Point(min, yMin), new Point(max, yMax));
    }
}
