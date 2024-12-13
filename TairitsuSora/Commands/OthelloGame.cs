using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Commands.Othello;
using TairitsuSora.Core;
using TairitsuSora.TairitsuSora.Commands.GameCommand;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class OthelloGame : TwoPlayerBoardGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "oth",
        DisplayName = "Othello",
        Summary = "黑白棋",
        Description = "黑与白的对立只是同一事物的两面罢了。"
    };

    [MessageHandler(Description = $"发起对局请求。输入坐标来下棋，{SubcommandDescription}")]
    public ValueTask StartGame(GroupMessageEventArgs ev) =>
        StartGame(ev, GameProcedureFactory((group, p1, p2) => CreateGameState(group, p1, p2, null)));

    [MessageHandler(
        Signature = "challenge $config",
        Description = $"向对立发起对局请求，$config 为对局时的配置，设为 \"help\" 可以查看所有配置选项以及有关人机对局的详细说明。" +
                      $"输入坐标来下棋，{SubcommandDescription}")]
    public async ValueTask ChallengeBot(GroupMessageEventArgs ev, string? config)
    {
        FluorineConfig fconf = await FluorineConfig.LoadAsync();
        if (config == "help")
        {
            await ShowConfigHelpAsync(ev);
            return;
        }
        config ??= fconf.Default;
        if (!fconf.Entries.TryGetValue(config, out var entry))
        {
            await ev.QuoteReply($"未找到配置 \"{config}\"");
            return;
        }
        await StartGame(ev,
            GameProcedureFactory(async (group, p1, p2) =>
                {
                    var state = CreateGameState(group, p1, p2, entry.ModelConfig);
                    await state.InitializeBotPlay();
                    return state;
                },
                Application.Instance.SelfId));
    }

    private GameState CreateGameState(long group, long player1, long player2, string? modelConfig) =>
        new(group, player1, player2, modelConfig);

    private async ValueTask ShowConfigHelpAsync(GroupMessageEventArgs ev)
    {
        var conf = await FluorineConfig.LoadAsync();
        const string description =
            "  以下列表中列出了目前所有可用的人机对战配置。" +
            "搜索深度的两个整数 (@x/y) 分别对应一般状态下的搜索深度以及终局时完全搜索的深度，对应配置在一众 AI 棋手中的 " +
            "ELO 分数也一并给出。所有配置不采用任何开局库，所以搜索深度较低或局面评估相对不精确的配置可能在开局就会脱谱" +
            "或出现重大失误。\n" +
            "  请注意，所有配置中采用的局面评估模型均为开发者从零开始通过自我对局训练得到，准确性不能保证与其他引擎或解析工具" +
            "有可比水平，因此相同搜索深度配置下的强度可能会与其他引擎有较大出入。另外，模型 ELO 由対立采用的众多模型匹配对战" +
            "结果计算而得，仅相对数值具有参考价值，绝对数值可能并不与其他衡量引擎或人类棋手水平的 ELO 标度等同。";
        StringBuilder sb = new($"说明：\n{description}\n配置选项：");
        foreach (var (name, entry) in conf.Entries)
        {
            string defaultSuffix = name == conf.Default ? "（默认）" : "";
            sb.Append($"\n  {name}{defaultSuffix}：{entry.Description}");
        }
        await ev.QuoteReply(sb.ToString());
    }

    private class GameState(long group, long player1, long player2, string? modelConfig)
        : TwoPlayerBoardGameState(group, player1, player2), IDisposable
    {
        public override string Player1Verb => "执黑";
        public override string Player2Verb => "执白";
        public override string Player1Noun => "黑方";
        public override string Player2Noun => "白方";

        public override string PleaseStartPrompt =>
            _selfId == Player1Id ? $"我已下 {_selfFirstMove!.Value.ToString()}，请白方继续。" : "请黑方开始。";

        public override bool Player1IsNext => _board.ActivePlayer == Board.CellType.Black;

        public override string GameSummary =>
            _moveHistory.Count == 0 ? "" : $"对局回放：{string.Join("", _moveHistory)}";

        public override bool IsMoveReply(string text) =>
            text is [(>= 'a' and <= 'h') or (>= 'A' and <= 'H'), >= '1' and <= '8'];

        public override ValueTask<byte[]> GenerateBoardImage() => ValueTask.FromResult(_drawer.DrawBoard(_board, true));

        public override async ValueTask<MoveResult> PlayMove(string message)
        {
            Board.Coords coords = Board.Coords.FromString(message);
            if (!_board.PlayableAt(coords))
                return new Illegal("目前不可在此处落子");
            _passed = false;
            _selfPlayed.Clear();
            await PlayMove(coords);
            if (_board.LegalMoves == 0)
            {
                await PlayMove(null);
                _passed = true;
            }
            while (!_board.Ended && SelfIsNext)
            {
                _passed = false;
                await PlayByModel();
                if (_board.LegalMoves == 0)
                    await PlayMove(null);
            }
            if (_selfPlayed.Count > 0)
                await Application.Api.SendGroupMessage(GroupId, $"我下 {string.Join(", ", _selfPlayed)}");
            return PlayMoveResult();
        }

        public async ValueTask InitializeBotPlay()
        {
            await _fluorine.LoadModelAsync(modelConfig!);
            if (_selfId != Player1Id) return;
            int[] candidateIdx = [19, 26, 37, 44];
            await PlayMove(_selfFirstMove = Board.Coords.FromIndex(candidateIdx.Sample()));
        }

        public override string DescribeState()
        {
            (int b, int w) = _board.CountDisks(false);
            string passedPrefix = _passed ? $"由于{NotNextPlayerNoun}无处可落子，" : "";
            return $"{passedPrefix}轮到{NextPlayerNoun}了，目前棋子数量：黑方 {b} - {w} 白方。";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _drawer.Dispose();
            _fluorine.Dispose();
        }

        private Board _board = new()
        {
            Black = 0x00000008_10000000ul,
            White = 0x00000010_08000000ul,
            LegalMoves = 0x00001020_04080000ul,
            ActivePlayer = Board.CellType.Black
        };
        private bool _passed;
        private long _selfId = Application.Instance.SelfId;
        private BoardDrawer _drawer = new();
        private FluorineSession _fluorine = new();
        private List<Board.Coords> _selfPlayed = [];
        private Board.Coords? _selfFirstMove;
        private List<Board.Coords> _moveHistory = [];

        private bool SelfIsNext =>
            (_board.ActivePlayer == Board.CellType.Black && _selfId == Player1Id) ||
            (_board.ActivePlayer == Board.CellType.White && _selfId == Player2Id);

        private MoveResult PlayMoveResult()
        {
            if (!_board.Ended) return new Ongoing();
            (int b, int w) = _board.CountDisks(true);
            string scores = $"（黑方 {b} - {w} 白方）";
            return b == w
                ? new Terminal($"双方打成平局{scores}")
                : new Terminal($"{(b > w ? "黑" : "白")}方胜出{scores}");
        }

        private async ValueTask PlayByModel()
        {
            if (_board.Mobility == 1)
            {
                var move = Board.Coords.FromIndex((int)ulong.TrailingZeroCount(_board.LegalMoves));
                _selfPlayed.Add(move);
                await PlayMove(move);
                return;
            }
            if (_board.TotalDisks > 10) // Do not randomize if more than 6 plies have been played
            {
                var move = await _fluorine.SuggestMoveAsync();
                _selfPlayed.Add(move);
                await PlayMove(move);
                return;
            }
            var analysis = await _fluorine.AnalyzeAsync();
            List<Board.Coords> candidates = [];
            foreach (var (move, score) in analysis)
                // All moves that lose by at most 2 points according to the model are considered
                if (analysis[0].score - score <= 1f)
                    candidates.Add(move);
            var selected = candidates.Sample();
            _selfPlayed.Add(selected);
            await PlayMove(selected);
        }

        private async ValueTask PlayMove(Board.Coords? coords)
        {
            _board = await _fluorine.PlayAsync(coords);
            if (coords is { } c)
                _moveHistory.Add(c);
        }
    }

    private record FluorineConfig(
        [property: JsonPropertyName("default")] string Default,
        [property: JsonPropertyName("configs")] Dictionary<string, FluorineConfig.Entry> Entries)
    {
        public record Entry(
            [property: JsonPropertyName("model_config")] string ModelConfig,
            [property: JsonPropertyName("description")] string Description
        );

        public static async ValueTask<FluorineConfig> LoadAsync()
        {
            const string path = "data/fluorine_config.json";
            return JsonSerializer.Deserialize<FluorineConfig>(await File.ReadAllTextAsync(path))!;
        }
    }
}
