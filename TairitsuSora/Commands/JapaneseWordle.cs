using SkiaSharp;
using Sora.EventArgs.SoraEvent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sora.Entities;
using TairitsuSora.Core;
using TairitsuSora.Utils;
using TairitsuSora.TairitsuSora.Commands.GameCommand;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class JapaneseWordle : GroupGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "jw",
        Summary = "日语猜词游戏",
        Description = "わかります。"
    };

    public override async ValueTask InitializeAsync()
    {
        _words = JsonSerializer.Deserialize<List<List<WordInfo>>>(
            await File.ReadAllTextAsync("data/meanings.json"))!;
        foreach (var list in _words)
            foreach (WordInfo word in list)
                word.target = NormalizeToHiragana(word.target);
        _allValidWords = (await File.ReadAllLinesAsync("data/jp-wordle-full.txt"))
            .Select(w => NormalizeToHiragana(w.Trim())).ToArray();
        Console.WriteLine($"[Japanese Wordle] Read {_words.Count} answer words and {_allValidWords.Length} valid words");
        await base.InitializeAsync();
    }

    [MessageHandler(Description = "开始游戏")]
    public ValueTask MainCommand(GroupMessageEventArgs ev) => StartGame(ev, GameProcedure);

    [MessageHandler(Signature = "end", Description = "结束当前游戏")]
    public async ValueTask DummyEnd(GroupMessageEventArgs ev) => await CancelGame(ev);

    private async ValueTask GameProcedure(GroupMessageEventArgs ev, CancellationToken token)
    {
        bool IsGameReply(GroupMessageEventArgs reply)
        {
            if (reply.SourceGroup.Id != ev.SourceGroup.Id) return false;
            if (reply.Message.MessageBody.GetIfOnlyText() is not { } text) return false;
            return IsGuessingWord(text) && _allValidWords.Contains(NormalizeToHiragana(text));
        }

        GameState game = new(_words.Sample());
        while (true)
        {
            await ev.Reply(new MessageBody().Image(game.DrawStateAsPng()));
            var next = await Application.EventChannel.WaitNextGroupMessage(
                next => next.FromSameGroup(ev) && IsGameReply(next), token);
            if (next is null) break;
            string text = next.Message.MessageBody.GetIfOnlyText()!.Trim();
            if (game.Guess(text) || game.GuessCount >= 12) break;
        }

        await ev.Reply(new MessageBody()
            .Image(game.DrawStateAsPng())
            .Text("游戏结束！本题答案：" + DescribeWord(game.Word)));
    }

#pragma warning disable CS0414, CS0649, CS8618
    private class WordInfo
    {
        [JsonInclude] public string target;
        [JsonInclude] public string jp;
        [JsonInclude] public string kana;
        [JsonInclude] public List<List<string>> en;
        [JsonInclude] public bool common;
        [JsonInclude] public List<string> jlpt;
    }
#pragma warning restore CS0414, CS0649, CS8618

    private class GameState(List<WordInfo> word)
    {
        public List<WordInfo> Word { get; } = word;
        public int GuessCount => _guesses.Count;

        public bool Guess(ReadOnlySpan<char> guessed)
        {
            string normGuess = NormalizeToHiragana(guessed);
            _guesses.Add(normGuess.Select(ch => new CharState(ch, CharColor.Black)).ToArray());
            CharState[] state = _guesses[^1];
            string target = Word[0].target;
            for (int i = 0; i < 4; i++)
                if (normGuess[i] == target[i])
                    state[i].Color = CharColor.Green;
            for (int i = 0; i < 4; i++)
            {
                int yellowCount =
                    target.Count(ch => ch == target[i] && state[i].Color != CharColor.Green);
                for (int j = 0; j < 4 && yellowCount > 0; j++)
                    if (state[j].Color != CharColor.Green && normGuess[j] == target[i])
                    {
                        state[j].Color = CharColor.Yellow;
                        yellowCount--;
                    }
            }
            return normGuess == target;
        }

        public byte[] DrawStateAsPng()
        {
            using ImageDrawer drawer = new(this);
            return drawer.DrawAsPng();
        }

        private enum CharColor { White, Black, Yellow, Green }
        private record struct CharState(char Ch, CharColor Color);

        private class ImageDrawer : IDisposable
        {
            public ImageDrawer(GameState state)
            {
                _state = state;
                _bitmap = new SKBitmap(new SKImageInfo(CellSize * 11, ImageHeight, SKColorType.Rgba8888));
                _canvas = new SKCanvas(_bitmap);
                _canvas.Clear(SKColors.Black);
                _paint.TextAlign = SKTextAlign.Center;
            }

            public byte[] DrawAsPng()
            {
                for (int i = 0; i < 12; i++)
                {
                    int xOffset = (i / 6 * 5 + 1) * CellSize;
                    int yOffset = (i % 6 + 1) * CellSize;
                    if (i < _state._guesses.Count)
                    {
                        CharState[] guess = _state._guesses[i];
                        for (int j = 0; j < 4; j++)
                            DrawChar(guess[j], xOffset + j * CellSize, yOffset);
                    }
                    else
                        for (int j = 0; j < 4; j++)
                            DrawBlock(CharColor.Black, xOffset + j * CellSize, yOffset);
                }
                DrawCandidates();

                using MemoryStream ms = new();
                _bitmap.Encode(ms, SKEncodedImageFormat.Png, quality: 100);
                return ms.ToArray();
            }

            public void Dispose()
            {
                _canvas.Dispose();
                _bitmap.Dispose();
                _paint.Dispose();
            }

            private const int CellSize = 50;
            private const int FontSize = 28;
            private const int FontBaseline = 35;
            private const int BlockSize = 40;
            private const int BlockOffset = (CellSize - BlockSize) / 2;
            private const int BlockInnerSize = 36;
            private const int BlockInnerOffset = (BlockSize - BlockInnerSize) / 2 + BlockOffset;
            private const int CornerRadius = 6;
            private const int CandidateFontSize = 20;
            private const int CandidateBlockSize = CellSize * 9 / 18;
            private const int CandidateBaseline = 35;
            private const int ImageHeight = 8 * CellSize + 4 * CandidateBlockSize + CandidateBaseline;
            private static readonly SKTypeface TypeFace = SKTypeface.FromFile("data/NotoSans.otf");
            private static readonly SKFont BlockFont = new(TypeFace, FontSize);
            private static readonly SKFont CandidateFont = new(TypeFace, CandidateFontSize);
            private static readonly SKColor White = new(235, 235, 235);
            private static readonly SKColor DarkText = new(100, 100, 100);
            private static readonly SKColor Yellow = new(234, 179, 8);
            private static readonly SKColor Green = new(34, 197, 94);
            private GameState _state;
            private SKPaint _paint = new() { IsAntialias = true, TextAlign = SKTextAlign.Center };
            private SKBitmap _bitmap;
            private SKCanvas _canvas;

            private void DrawBlock(CharColor color, int x, int y)
            {
                SKRoundRect outerRect = new(new SKRect(
                    x + BlockOffset, y + BlockOffset,
                    x + BlockOffset + BlockSize, y + BlockOffset + BlockSize),
                    CornerRadius);

                if (color == CharColor.Black)
                {
                    _paint.Color = White;
                    SKRoundRect innerRect = new(new SKRect(
                        x + BlockInnerOffset, y + BlockInnerOffset,
                        x + BlockInnerOffset + BlockInnerSize, y + BlockInnerOffset + BlockInnerSize),
                        CornerRadius);
                    _canvas.DrawRoundRectDifference(outerRect, innerRect, _paint);
                }
                else
                {
                    _paint.Color = color == CharColor.Yellow ? Yellow : Green;
                    _canvas.DrawRoundRect(outerRect, _paint);
                }
            }

            private void DrawChar(CharState ch, int x, int y)
            {
                DrawBlock(ch.Color, x, y);
                _paint.Color = White;
                _canvas.DrawText(ch.Ch.ToString(), x + CellSize / 2, y + FontBaseline, BlockFont, _paint);
            }

            private void DrawCandidateChar(char ch, CharColor color, int x, int y)
            {
                _paint.Color = color switch
                {
                    CharColor.Black => DarkText,
                    CharColor.Yellow => Yellow,
                    CharColor.Green => Green,
                    CharColor.White => White,
                    _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
                };
                _canvas.DrawText(ch.ToString(),
                    CellSize + (2 * x + 1) * CandidateBlockSize / 2,
                    7 * CellSize + y * CandidateBlockSize + CandidateBaseline,
                    CandidateFont, _paint);
            }

            private void DrawCandidates()
            {
                Dictionary<char, CharColor> colors = new();
                foreach (char ch in ShownCandidates) colors[ch] = CharColor.White;
                foreach (var guess in _state._guesses)
                    foreach (var ch in guess)
                        colors[ch.Ch] = (CharColor)Math.Max((int)colors[ch.Ch], (int)ch.Color);
                for (int i = 0; i < ShownCandidates.Length; i++)
                    DrawCandidateChar(ShownCandidates[i], colors[ShownCandidates[i]], i / 5, i % 5);
            }
        }

        private const string ShownCandidates =
            "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもや ゆ よらりるれろわ   を" +
            "ぁぃぅぇぉがぎぐげござじずぜぞだぢづでどぱぴぷぺぽばびぶべぼゃ ゅ ょーっゔん ";
        private List<CharState[]> _guesses = [];
    }

    private const string AllCandidates =
        "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんー" +
        "ぁぃぅぇぉがぎぐげござじずぜぞだぢづでどぱぴぷぺぽばびぶべぼゃゅょっゔ";
    private const string Katakana =
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンー" +
        "ァィゥェォガギグゲゴザジズゼゾダヂヅデドパピプペポバビブベボャュョッヴ";
    private List<List<WordInfo>> _words = [];
    private string[] _allValidWords = null!;

    private static bool IsGuessingWord(string word)
        => word.Length == 4 && word.All(ch => AllCandidates.Contains(ch) || Katakana.Contains(ch));

    private static string NormalizeToHiragana(ReadOnlySpan<char> word)
    {
        char[] res = word.ToArray();
        for (int i = 0; i < word.Length; i++)
        {
            int j = Katakana.IndexOf(word[i]);
            if (j != -1) res[i] = AllCandidates[j];
        }
        return new string(res);
    }

    private static string DescribeWord(List<WordInfo> list)
    {
        StringBuilder sb = new(list[0].target);
        WordInfo[] filtered = list.Where(w => w.common).ToArray();
        if (filtered.Length == 0 && list.Count != 0) filtered = [list[0]];
        foreach (WordInfo word in filtered)
        {
            sb.Append($"\n{word.jp}: {word.en[0][0]}");
            if (word.jlpt.Count > 0) sb.Append($" [{string.Join(", ", word.jlpt)}]");
        }
        return sb.ToString();
    }
}
