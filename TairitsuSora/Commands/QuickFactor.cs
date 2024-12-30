using Sora.EventArgs.SoraEvent;
using System.Numerics;
using System.Text;
using LanguageExt.UnitsOfMeasure;
using TairitsuSora.Core;
using TairitsuSora.TairitsuSora.Commands.GameCommand;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class QuickFactor : GroupGame
{
    public override CommandInfo Info => new()
    {
        Trigger = "qf",
        Summary = "素因数分解题",
        Description =
            "看看你的数感有多强。我会随机给出一些合数，答题者需写出这个数的素因数分解，每 3 题难度会上升一级。" +
            "作答时请用空格分割各个素数（maxp < 11 时也可不用空格分隔），也可用“[素数]p[指数]”的形式简写幂次。" +
            "答错无惩罚。规定时间以内未作答正确测试即结束。你能答到第几题呢？\n" +
            "示例：[题目] Q1: 72 = ? [回答 1] 2 2 2 3 3 [回答 2] 2p3 3p2 [回答 3] 22233"
    };

    [MessageHandler(Signature = "$maxp $time", Description =
        "开始答题，[maxp = 5..1000] 为最大可能的素因子，[timeSpan = 5s..1min] 为每题给的时间")]
    public ValueTask MainCommand(GroupMessageEventArgs ev, int maxp = 7, [ShowDefaultValueAs("10s")] TimeSpan? time = null)
        => StartGame(ev, (e, _) => GameProcedure(e, maxp, time ?? 10.Seconds()));

    private const int MaxPrime = 1000;
    private static int[] _primes = FindPrimesSieve(MaxPrime);

    private async ValueTask GameProcedure(
        GroupMessageEventArgs ev, int maxp, TimeSpan time)
    {
        if (maxp is < 5 or > MaxPrime)
        {
            await ev.QuoteReply("maxp 需在 5 到 1000 之间");
            return;
        }
        if (time.TotalSeconds is < 5 or > 60)
        {
            await ev.QuoteReply("time 需在 5s 到 1min 之间");
            return;
        }
        await ev.QuoteReply("请准备好，测试将在 3 秒后开始...");
        await Task.Delay(3000);
        int maxi = GetPrimeSubsetLength(maxp);
        for (int i = 0; i < 50; i++)
        {
            (BigInteger q, SortedDictionary<int, int> a) = GenerateQA(i / 3, maxi);
            await ev.Reply($"Q{i + 1}: {q:N0}");
            if (await Application.EventChannel.WaitNextGroupMessage(
                    next => next.FromSameMember(ev) && CheckAnswer(a, next.Message.MessageBody.GetIfOnlyText()),
                    time) is not null)
                continue;
            await ev.Reply($"最终分数: {i}\nA{i + 1}: {q:N0} = {FormatAnswer(a)}");
            return;
        }
        await ev.Reply("开挂实锤，我麻了");
    }

    private int GetPrimeSubsetLength(int maxp)
    {
        for (int i = 0; i < _primes.Length; i++)
            if (_primes[i] > maxp)
                return i;
        return _primes.Length;
    }

    private (BigInteger, SortedDictionary<int, int>) GenerateQA(int difficulty, int maxi)
    {
        BigInteger composite = 1;
        SortedDictionary<int, int> answer = [];
        for (int i = 0; i < difficulty + 3; i++)
        {
            int prime = _primes[Random.Shared.Next(maxi)];
            AddToKey(answer, prime, 1);
            composite *= prime;
        }
        return (composite, answer);
    }

    private bool CheckAnswer(SortedDictionary<int, int> answer, string? text)
    {
        if (text is null) return false;
        string[] components = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (components.Length == 1) // Separate into digits
            components = components[0].Select(ch => ch.ToString()).ToArray();
        SortedDictionary<int, int> parsedAnswer = [];
        foreach (string comp in components)
        {
            int indexOfP = comp.IndexOf('p');
            if (indexOfP == -1) // Only number
            {
                if (int.TryParse(comp, out int res))
                    AddToKey(parsedAnswer, res, 1);
                else
                    return false;
            }
            else
            {
                if (int.TryParse(comp[..indexOfP], out int b) &&
                    int.TryParse(comp[(indexOfP + 1)..], out int e))
                    AddToKey(parsedAnswer, b, e);
                else
                    return false;
            }
        }
        return parsedAnswer.SequenceEqual(answer, KeyValuePairComparer<int, int>.Instance);
    }

    private void AddToKey(SortedDictionary<int, int> dict, int key, int increment)
    {
        if (!dict.TryAdd(key, increment))
            dict[key] += increment;
    }

    private string FormatAnswer(SortedDictionary<int, int> answer)
    {
        const string superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        StringBuilder sb = new();
        bool first = true;
        foreach (var kv in answer)
        {
            if (!first)
                sb.Append('×');
            else
                first = false;
            sb.Append(kv.Key);
            if (kv.Value != 1)
                sb.Append(kv.Value.ToString().Select(ch => superscripts[ch - '0']).ToArray());
        }
        return sb.ToString();
    }

    private static int[] FindPrimesSieve(int maxPrime)
    {
        bool[] sieve = new bool[maxPrime + 1];
        Array.Fill(sieve, true);
        sieve[0] = sieve[1] = false;
        for (int i = 2; i * i <= maxPrime; i++)
            for (int j = 2 * i; j <= maxPrime; j += i)
                sieve[j] = false;
        return sieve.Select((prime, i) => (prime, i))
            .Where(pair => pair.prime)
            .Select(pair => pair.i)
            .ToArray();
    }
}
