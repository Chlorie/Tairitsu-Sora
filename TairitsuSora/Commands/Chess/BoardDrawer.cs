using SkiaSharp;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.Chess;

public class BoardDrawerResources(HttpClient httpClient, string dataPath)
{
    private const string ChessComThemesUriBase = "https://images.chesscomfiles.com/chess-themes/";

    private byte[]? _boardCache;
    private Dictionary<Piece, byte[]> _pieceCache = new();

    private async ValueTask<byte[]> GetImage(string path, string url)
    {
        string extendedPath = Path.Combine(dataPath, path);
        string fullUrl = ChessComThemesUriBase + url;
        if (File.Exists(extendedPath))
            return await File.ReadAllBytesAsync(extendedPath);
        var bytes = await httpClient.GetByteArrayAsync(fullUrl);
        new FileInfo(extendedPath).Directory?.Create();
        await File.WriteAllBytesAsync(extendedPath, bytes);
        return bytes;
    }

    public async ValueTask<byte[]> GetBoard() => _boardCache ??= await GetBoardImpl();

    public async ValueTask<byte[]> GetPiece(Piece piece)
    {
        lock (_pieceCache)
            if (_pieceCache.TryGetValue(piece, out byte[]? pieceImage))
                return pieceImage;
        byte[] res = await GetPieceImpl(piece);
        lock (_pieceCache) _pieceCache.Add(piece, res);
        return res;
    }

    private async ValueTask<byte[]> GetBoardImpl() => await GetImage("chess-board.png", "boards/green/40.png");

    private async ValueTask<byte[]> GetPieceImpl(Piece piece)
    {
        string fileName = $"{(piece.Color == Color.White ? 'w' : 'b')}{char.ToLower(piece.FenRepr)}";
        string localFile = $"chess-piece-{fileName}.png";
        string remoteFile = $"pieces/neo/60/{fileName}.png";
        return await GetImage(localFile, remoteFile);
    }
}

public class BoardDrawer : IDisposable
{
    public BoardDrawer(BoardDrawerResources res)
    {
        _res = res;
        _bitmap = new SKBitmap(new SKImageInfo(8 * CellSize, 8 * CellSize, SKColorType.Rgba8888));
        _canvas = new SKCanvas(_bitmap);
    }

    public async ValueTask<byte[]> DrawAsPng(Board board, Color perspective, IEnumerable<Coords>? highlightedCells = null)
    {
        await DrawEmptyBoard(perspective);
        if (highlightedCells != null)
            foreach (var cell in highlightedCells)
                DrawHighlight(cell);
        await DrawPieces(board);
        using MemoryStream ms = new();
        _bitmap.Encode(ms, SKEncodedImageFormat.Png, quality: 100);
        return ms.ToArray();
    }

    public ValueTask<byte[]> DrawAsPng(Game game, IEnumerable<Coords>? highlightedCells = null)
        => DrawAsPng(game.Position, game.Player, highlightedCells);

    public void Dispose()
    {
        _paint.Dispose();
        _canvas.Dispose();
        _bitmap.Dispose();
    }

    private const int CellSize = 60;
    private const int FontSize = 12;
    private static readonly SKPoint RankOffset = new(5, 15);
    private static readonly SKPoint FileOffset = new(-5, -5);
    private static readonly SKColor DarkGreen = new(118, 150, 86);
    private static readonly SKColor LightGreen = new(238, 238, 210);
    private static readonly SKColor HighlightYellow = new(255, 255, 0, 127);
    private static readonly SKTypeface TypeFace = SKTypeface.FromFile("data/NotoSans.otf");
    private static readonly SKFont CoordsFont = new(TypeFace, FontSize);

    private BoardDrawerResources _res;
    private Color _perspective = Color.White;
    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private SKPaint _paint = new(CoordsFont) { IsAntialias = true, TextAlign = SKTextAlign.Center };

    private async ValueTask DrawEmptyBoard(Color perspective)
    {
        _perspective = perspective;
        byte[] board = await _res.GetBoard();
        using SKImage boardImg = SKImage.FromEncodedData(board);
        _canvas.DrawImage(boardImg, boardImg.Info.Rect, _bitmap.Info.Rect);
        DrawCoords();
    }

    private void DrawHighlight(Coords cell)
    {
        int y = (_perspective == Color.White ? 7 - cell.Rank : cell.Rank) * CellSize;
        int x = (_perspective == Color.Black ? 7 - cell.File : cell.File) * CellSize;
        _paint.Color = HighlightYellow;
        _paint.Style = SKPaintStyle.Fill;
        _canvas.DrawRect(x, y, CellSize, CellSize, _paint);
    }

    private async ValueTask DrawPieces(Board board)
    {
        Dictionary<Piece, SKImage> pieceImgs = new();
        using var guard = pieceImgs.Values.Disposer();

        foreach (var color in Enum.GetValues<Color>())
            foreach (var type in Enum.GetValues<PieceType>())
            {
                if (type == PieceType.None) continue;
                Piece piece = new(color, type);
                byte[] bytes = await _res.GetPiece(new Piece(color, type));
                pieceImgs.Add(piece, SKImage.FromEncodedData(bytes));
            }

        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
            {
                Piece piece = board[new Coords(i, j)];
                if (piece.Type == PieceType.None) continue;
                int y = (_perspective == Color.White ? 7 - i : i) * CellSize;
                int x = (_perspective == Color.Black ? 7 - j : j) * CellSize;
                SKImage pieceImg = pieceImgs[piece];
                _canvas.DrawImage(pieceImg, pieceImg.Info.Rect, SKRect.Create(x, y, CellSize, CellSize));
            }
    }

    private void DrawCoords()
    {
        _paint.Style = SKPaintStyle.Fill;
        for (int i = 0; i < 8; i++)
        {
            string rank = (_perspective == Color.White ? 8 - i : i + 1).ToString();
            _paint.Color = i % 2 == 0 ? DarkGreen : LightGreen;
            _paint.TextAlign = SKTextAlign.Left;
            _canvas.DrawText(rank, new SKPoint(0, i * CellSize) + RankOffset, _paint);

            string file = ((char)(_perspective == Color.Black ? 7 - i + 'a' : i + 'a')).ToString();
            _paint.Color = i % 2 == 0 ? LightGreen : DarkGreen;
            _paint.TextAlign = SKTextAlign.Right;
            _canvas.DrawText(file, new SKPoint((i + 1) * CellSize, 8 * CellSize) + FileOffset, _paint);
        }
    }
}
