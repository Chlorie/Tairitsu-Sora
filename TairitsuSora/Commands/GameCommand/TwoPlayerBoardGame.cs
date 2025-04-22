using LanguageExt.UnitsOfMeasure;
using Sora.Entities;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.TairitsuSora.Commands.GameCommand;

public abstract class TwoPlayerBoardGame : GroupGame
{
    protected const string SubcommandDescription = "输入 s 重新显示棋盘，输入 r 认输，输入 d 求和或同意求和。";

    protected static Func<GroupMessageEventArgs, CancellationToken, ValueTask> GameProcedureFactory(
        Func<long, long, long, TwoPlayerBoardGameState> gameStateFactory, long? specifiedTarget = null) =>
        GameProcedureFactory((group, p1, p2) =>
            ValueTask.FromResult(gameStateFactory(group, p1, p2)), specifiedTarget);

    protected static Func<GroupMessageEventArgs, CancellationToken, ValueTask> GameProcedureFactory(
        Func<long, long, long, ValueTask<TwoPlayerBoardGameState>> gameStateFactory, long? specifiedTarget = null)
    {
        async ValueTask GameProcedureAsync(GroupMessageEventArgs ev, CancellationToken _)
        {
            long p1, p2;
            // TODO: do not accept by default in the case that the specified target is not the bot
            if (specifiedTarget is { } target)
                (p1, p2) = (ev.SenderInfo.UserId, target);
            else
            {
                var players = await GetPlayersAsync(ev);
                if (players is null) return;
                (p1, p2) = players.Value;
            }
            if (Random.Shared.Next(2) == 0) (p1, p2) = (p2, p1);
            TwoPlayerBoardGameState state = await gameStateFactory(ev.SourceGroup.Id, p1, p2);
            await PlayGameAsync(state);
        }

        return GameProcedureAsync;
    }

    private static async ValueTask<(long p1, long p2)?> GetPlayersAsync(GroupMessageEventArgs ev)
    {
        await ev.QuoteReply("已发起对局请求，2 分钟内回复 “a” 即可开始对局。");
        var accept = await Application.EventChannel.WaitNextGroupMessage(
            next => next.FromSameGroup(ev) && next.Message.MessageBody.GetIfOnlyText() == "a",
            2.Minutes());
        if (accept is null)
        {
            await ev.QuoteReply("2 分钟内无人接受挑战，自动取消。");
            return null;
        }
        if (accept.FromSameMember(ev) && !Application.Instance.Admins.Contains(accept.Sender.Id))
        {
            await accept.QuoteReply("您好，我这里不提供左右互搏服务呢。");
            return null;
        }
        return (ev.SenderInfo.UserId, accept.SenderInfo.UserId);
    }

    private static async ValueTask PlayGameAsync(TwoPlayerBoardGameState state)
    {
        long group = state.GroupId, player1 = state.Player1Id, player2 = state.Player2Id;
        using var guard = new MaybeDisposable(state);

        await Application.Api.SendGroupMessage(group, new MessageBody()
            .Text("对局开始！").At(player1).Text($"{state.Player1Verb}，")
            .At(player2).Text($"{state.Player2Verb}。{state.PleaseStartPrompt}")
            .Image(await state.GenerateBoardImage()));

        bool IsGameReply(GroupMessageEventArgs ev) =>
            ev.SourceGroup.Id == group &&
            (ev.SenderInfo.UserId == player1 || ev.SenderInfo.UserId == player2) &&
            ev.Message.MessageBody.GetIfOnlyText() is { } text &&
            (text is "s" or "d" or "r" || state.IsMoveReply(text));

        async ValueTask ShowBoard()
            => await Application.Api.SendGroupMessage(group, new MessageBody()
                .Text($"{state.DescribeState()}当前状态：").Image(await state.GenerateBoardImage()));

        async ValueTask GameEnds(string reason) =>
            await Application.Api.SendGroupMessage(group, new MessageBody()
                .Text($"{reason}，对局结束。最终状态：")
                .Image(await state.GenerateBoardImage())
                .Text(state.GameSummary));

        bool p1Draw = false, p2Draw = false;
        MoveTimeLimit? timeLimit = state.TimeLimit;
        DateTime startTime = DateTime.Now;
        while (true)
        {
            TimeSpan delay = Timeout.InfiniteTimeSpan;
            if (timeLimit is { Seconds: var limit })
                delay = startTime.AddSeconds(limit) - DateTime.Now;
            if (delay.Ticks <= 0)
            {
                await GameEnds($"{state.NextPlayerNoun}时间耗尽判负");
                return;
            }
            using CancellationTokenSource src = new();
            var timeNoticeTask = Task.CompletedTask;
            if (timeLimit is { NoticeSeconds: not null })
                timeNoticeTask = SendTimeLimitNotice(group, startTime, timeLimit, src.Token).AsTask();
            var waitMsgTask = Application.EventChannel.WaitNextGroupMessage(IsGameReply, delay).AsTask();
            await Task.WhenAny(timeNoticeTask, waitMsgTask);
            await src.CancelAsync();
            GroupMessageEventArgs? ev = await waitMsgTask;
            if (ev is null)
            {
                await GameEnds($"{state.NextPlayerNoun}时间耗尽判负");
                return;
            }

            bool isPlayer1 = ev.SenderInfo.UserId == player1;
            string text = ev.Message.MessageBody.GetIfOnlyText()!;
            switch (text)
            {
                case "r":
                {
                    string playerNoun = isPlayer1 ? state.Player1Noun : state.Player2Noun;
                    await GameEnds($"由于{playerNoun}认输");
                    return;
                }
                case "s":
                {
                    await ShowBoard();
                    continue;
                }
                case "d":
                {
                    if (isPlayer1) p1Draw = true;
                    else p2Draw = true;
                    if (p1Draw && p2Draw)
                    {
                        await GameEnds("由于双方同意平局");
                        return;
                    }
                    await Application.Api.SendGroupMessage(group, new MessageBody()
                        .At(ev.Sender.Id).Text("发出了求和申请。"));
                    continue;
                }
            }
            if (ev.SenderInfo.UserId != (state.Player1IsNext ? player1 : player2))
            {
                await ev.QuoteReply("你先别急");
                continue;
            }
            p1Draw = p2Draw = false;
            var moveResult = await state.PlayMove(text);
            switch (moveResult)
            {
                case Ongoing:
                    await ShowBoard();
                    startTime = DateTime.Now;
                    continue;
                case Terminal terminal:
                    await GameEnds(terminal.Result);
                    return;
                case Illegal illegal:
                    await ev.QuoteReply(illegal.Message);
                    continue;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static async ValueTask SendTimeLimitNotice(
        long group, DateTime start, MoveTimeLimit timeLimit, CancellationToken token)
    {
        if (timeLimit.NoticeSeconds is null or { Count: 0 }) return;
        List<long> notices = [.. timeLimit.NoticeSeconds];
        notices.Sort((l, r) => r.CompareTo(l));
        DateTime final = start.AddSeconds(timeLimit.Seconds);
        DateTime[] deadlines = notices.Select(l => final.AddSeconds(-l)).ToArray();
        try
        {
            foreach (var (ddl, seconds) in deadlines.Zip(notices))
            {
                var delay = ddl - DateTime.Now;
                if (delay.Ticks <= 0) continue;
                await Task.Delay(delay, token);
                await Application.Api.SendGroupMessage(group, $"剩余 {seconds} 秒！");
            }
            await token.WaitUntilCanceled();
        }
        catch (OperationCanceledException) { }
    }
}

public abstract record MoveResult;

public record Ongoing : MoveResult;
public record Terminal(string Result) : MoveResult;
public record Illegal(string Message) : MoveResult;

public record MoveTimeLimit(long Seconds, List<long>? NoticeSeconds = null);

public abstract class TwoPlayerBoardGameState(long group, long player1, long player2)
{
    public abstract string Player1Verb { get; } // A verb phrase. e.g. "plays white"
    public abstract string Player2Verb { get; }
    public abstract string Player1Noun { get; } // A noun phrase e.g. "the white player"
    public abstract string Player2Noun { get; }
    public string NextPlayerNoun => Player1IsNext ? Player1Noun : Player2Noun;
    public string NotNextPlayerNoun => Player1IsNext ? Player2Noun : Player1Noun;
    public abstract string PleaseStartPrompt { get; }
    public virtual string GameSummary => "";
    public virtual MoveTimeLimit? TimeLimit => null;

    public long GroupId => group;
    public long Player1Id => player1;
    public long Player2Id => player2;
    public abstract bool Player1IsNext { get; }

    public abstract bool IsMoveReply(string text);
    public abstract ValueTask<byte[]> GenerateBoardImage();
    public abstract ValueTask<MoveResult> PlayMove(string message);
    public abstract string DescribeState();
}
