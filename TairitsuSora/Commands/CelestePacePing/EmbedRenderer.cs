using SkiaSharp;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands.CelestePacePing;

public class EmbedRenderer : IDisposable
{
    public EmbedRenderer(EmojiShortcodeConverter emojiConverter)
    {
        _emojiConverter = emojiConverter;
        _canvas = new SKCanvas(_bitmap);
    }

    public byte[] Render(DiscordWebhookRequest.Embed embed)
    {
        ClearAndDrawRibbon(embed.Color);
        DrawTitle(embed.Title);
        DrawAllFields(embed.Fields);
        return ClipAndEncode();
    }

    public void Dispose()
    {
        _paint.Dispose();
        _canvas.Dispose();
        _bitmap.Dispose();
    }

    private const string WhiteSquare = "\u2b1c";
    private const string GreenSquare = "\U0001f7e9";
    private const string BlackSquare = "\u2b1b";

    private const int ImageWidth = 600, MaxImageHeight = 800;
    private const int RibbonWidth = 7;
    private const int TitleFontSize = 24;
    private const int ContentFontSize = 20;
    private const int HorizontalMargin = 25;
    private const int TopMargin = 45, BottomMargin = -15;
    private const int ContentWidth = ImageWidth - 2 * HorizontalMargin;
    private const int SmallSeparator = 10;
    private const int LargeSeparator = 25;
    private const float ProgressBarHeight = 12f;
    private static readonly SKColor BackgroundColor = new(31, 31, 31);
    private static readonly SKColor TextColor = new(220, 220, 220);
    private static readonly SKColor ProgressUnfilledColor = new(85, 85, 85);
    private static readonly SKColor ProgressFilledColor = new(40, 200, 40);

    private static readonly SKTypeface RegularTypeFace = SKTypeface.FromFile("data/NotoSans.otf");
    private static readonly SKTypeface BoldTypeFace = SKTypeface.FromFile("data/NotoSans-Bold.otf");
    private static readonly SKFont TitleFont = new(BoldTypeFace, TitleFontSize);
    private static readonly SKFont ContentTitleFont = new(BoldTypeFace, ContentFontSize);
    private static readonly SKFont ContentFont = new(RegularTypeFace, ContentFontSize);

    private readonly EmojiShortcodeConverter _emojiConverter;
    private SKPaint _paint = new() { IsAntialias = true, TextAlign = SKTextAlign.Left };
    private SKBitmap _bitmap = new(new SKImageInfo(ImageWidth, MaxImageHeight, SKColorType.Rgba8888));
    private SKCanvas _canvas;
    private int _y;

    private void ClearAndDrawRibbon(int color)
    {
        _canvas.Clear(BackgroundColor);
        _paint.Color = new SKColor((uint)(color + 0xff000000));
        _canvas.DrawRect(0, 0, RibbonWidth, MaxImageHeight, _paint);
    }

    private void DrawTitle(string title)
    {
        _paint.Color = TextColor;
        _canvas.DrawText(title, HorizontalMargin, _y = TopMargin, TitleFont, _paint);
        _y += TitleFontSize + LargeSeparator;
    }

    private void DrawAllFields(List<DiscordWebhookRequest.Field> fields)
    {
        List<DiscordWebhookRequest.Field> line = [];
        foreach (var field in fields)
            if (field.Inline)
                line.Add(field);
            else
            {
                DrawFieldLine(line);
                line.Clear();
                DrawFieldLine([field]);
            }
        DrawFieldLine(line);
        _y += BottomMargin;
    }

    private void DrawFieldLine(List<DiscordWebhookRequest.Field> fields)
    {
        if (fields.Count == 0) return;
        float horizontal = ContentWidth / fields.Count;
        const int firstLineOffset = ContentFontSize + SmallSeparator;
        const int secondLineOffset = firstLineOffset + ContentFontSize + LargeSeparator;
        for (int i = 0; i < fields.Count; i++)
        {
            _paint.Color = TextColor;
            float x = HorizontalMargin + i * horizontal;
            _canvas.DrawText(fields[i].Name, x, _y, ContentTitleFont, _paint);
            string text = _emojiConverter.Convert(fields[i].Value);
            if (ParseProgressBar(text) is { } ratio)
                DrawProgressBar(ratio, _y + firstLineOffset);
            else
                _canvas.DrawText(text, x, _y + firstLineOffset, ContentFont, _paint);
        }
        _y += secondLineOffset;
    }

    private float? ParseProgressBar(ReadOnlySpan<char> text)
    {
        int filled = 0, unfilled = 0;
        while (true)
        {
            if (text.ConsumeIfStartsWith(WhiteSquare) ||
                text.ConsumeIfStartsWith(GreenSquare))
                filled++;
            else if (text.ConsumeIfStartsWith(BlackSquare))
                unfilled++;
            else
                break;
        }
        return filled + unfilled == 0 ? null : (float)filled / (filled + unfilled);
    }

    private void DrawProgressBar(float ratio, float y)
    {
        const float radius = ProgressBarHeight / 2f;
        float top = y + (ContentFont.Metrics.Ascent + ContentFont.Metrics.Descent) / 2f - radius;

        SKRoundRect fullBar = new(SKRect.Create(HorizontalMargin, top, ContentWidth, ProgressBarHeight), radius);
        _paint.Color = ProgressUnfilledColor;
        _canvas.DrawRoundRect(fullBar, _paint);

        SKPath filledRect = new();
        filledRect.AddRect(SKRect.Create(HorizontalMargin, top, ContentWidth * ratio, ProgressBarHeight));
        SKPath filledPath = new();
        filledPath.AddRoundRect(fullBar);
        filledPath = filledPath.Op(filledRect, SKPathOp.Intersect);
        _paint.Color = ProgressFilledColor;
        _canvas.DrawPath(filledPath, _paint);
    }

    private byte[] ClipAndEncode()
    {
        _canvas.DrawRect(0, _y, ImageWidth, MaxImageHeight - _y, _paint);
        var prevBlend = _paint.BlendMode;
        _paint.BlendMode = SKBlendMode.Src;
        _paint.Color = SKColors.Transparent;
        _canvas.DrawRoundRectDifference(
            new SKRoundRect(SKRect.Create(ImageWidth, MaxImageHeight)),
            new SKRoundRect(SKRect.Create(ImageWidth, _y), RibbonWidth), _paint);
        _paint.BlendMode = prevBlend;
        using SKBitmap clipped = new(new SKImageInfo(ImageWidth, _y, SKColorType.Rgba8888));
        _bitmap.ExtractSubset(clipped, SKRectI.Create(ImageWidth, _y));
        using MemoryStream ms = new();
        clipped.Encode(ms, SKEncodedImageFormat.Png, quality: 100);
        return ms.ToArray();
    }
}
