using System.Numerics;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class QuickMafs : GroupGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "qm",
        Summary = "口 算 题",
        Description =
            "做一做口算题预防老年痴呆（确信）。我会随机出一些口算题，每 5 题难度会上升一级。" +
            "作答时直接发送答案即可，答错无惩罚。规定时间以内未作答正确测试即结束。你能答到第几题呢？"
    };

    [MessageHandler(Signature = "$time", Description = "开始答题，每道题作答时间为 [time = 5~30s]")]
    public ValueTask MainCommand(GroupMessageEventArgs ev, [ShowDefaultValueAs("10s")] TimeSpan? time = null)
        => StartGame(ev, (e, _) => GameProcedure(e, time ?? TimeSpan.FromSeconds(10)));

    private async ValueTask GameProcedure(GroupMessageEventArgs ev, TimeSpan time)
    {
        if (time.TotalSeconds is < 5 or > 30)
        {
            await ev.QuoteReply("time 需在 5s 到 30s 之间");
            return;
        }
        await ev.QuoteReply("请准备好，测试将在 3 秒后开始...");
        await Task.Delay(3000);
        for (int i = 0; i < 50; i++)
        {
            (string q, string a) = GenerateQA(i / 5);
            await ev.Reply($"Q{i + 1}: {q}");
            if (await Application.EventChannel.WaitNextGroupMessage(
                   next => next.FromSameMember(ev) && next.Message.MessageBody.GetIfOnlyText() == a,
                   time) is not null)
                continue;
            await ev.Reply($"最终分数: {i}\nA{i + 1}: {q} {a}");
            return;
        }
        await ev.Reply("开挂实锤，我麻了");
    }

    private static BigInteger RandBigIntWithNDigits(int digits)
    {
        int RandIntWithNDigits(int d)
        {
            if (d == 0) return 0;
            int min = (int)Math.Pow(10, d - 1);
            return Random.Shared.Next(min, min * 10 - 1);
        }

        BigInteger res = RandIntWithNDigits(digits % 9);
        for (int i = 0; i < digits / 9; i++)
            res = res * 1_000_000_000 + RandIntWithNDigits(9);
        return res;
    }

    private static (string, string) GenerateQA(int difficulty)
    {
        int digits1 = (difficulty + 3) / 2, digits2 = (difficulty + 2) / 2;
        switch (Random.Shared.Next(2))
        {
            case 0: // + or -
            {
                int digits = digits1 * digits2;
                var a = RandBigIntWithNDigits(digits);
                var b = RandBigIntWithNDigits(digits);
                return Random.Shared.Next(2) switch
                {
                    0 => ($"{a} + {b} =", (a + b).ToString()),
                    1 => ($"{a + b} - {a} =", b.ToString()),
                    _ => throw new InvalidOperationException()
                };
            }
            case 1:
            {
                var a = RandBigIntWithNDigits(digits1);
                var b = RandBigIntWithNDigits(digits2);
                return Random.Shared.Next(2) switch
                {
                    0 => ($"{a} × {b} =", (a * b).ToString()),
                    1 => ($"{a * b} / {b} =", a.ToString()),
                    _ => throw new InvalidOperationException()
                };
            }
        }
        throw new InvalidOperationException();
    }
}
