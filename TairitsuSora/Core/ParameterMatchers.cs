using Sora.Entities;
using Sora.Entities.Segment;
using Sora.Entities.Segment.DataModel;
using LanguageExt;
using static LanguageExt.Prelude;
using TairitsuSora.Utils;

namespace TairitsuSora.Core;

public class IntParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(int);
    public override string ShownTypeName => "int";

    protected override Option<Any> TryMatchToken(SoraSegment segment)
        => segment.GetText() is { } text && int.TryParse(text, out int value)
            ? value.ToAny() : None;
}

public class FloatParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(float);
    public override string ShownTypeName => "float";

    protected override Option<Any> TryMatchToken(SoraSegment segment)
        => segment.GetText() is { } text && float.TryParse(text, out float value)
            ? value.ToAny() : None;
}

public class StringParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(string);
    public override string ShownTypeName => "string";

    protected override Option<Any> TryMatchToken(SoraSegment segment)
        => segment.GetText() is { } text && text != "" ? text.ToAny() : None;
}

public record struct FullString(string Text);

public class FullStringParameterMatcher : IParameterMatcher
{
    public Type ParameterType => typeof(FullString);
    public string ShownTypeName => "string+";

    public Either<Any, MessageBody> TryMatch(ref MessageBody msg)
    {
        if (msg.Count == 0 || msg[0].GetText() is not { } text)
            return new FullString("").ToAny();
        msg.RemoveAt(0);
        return new FullString(text).ToAny();
    }
}

public class TimeSpanParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(TimeSpan);
    public override string ShownTypeName => "TimeSpan";

    protected override Option<Any> TryMatchToken(SoraSegment segment)
    {
        string? text = segment.GetText();
        if (string.IsNullOrWhiteSpace(text)) return None;
        int idx = text.LastIndexOfAny("+-.e0123456789".ToCharArray()) + 1;
        if (idx == 0) return None;
        if (!float.TryParse(text[..idx], out float count)) return None;
        try
        {
            TimeSpan? res = text[idx..] switch
            {
                "ms" => TimeSpan.FromMilliseconds(count),
                "s" => TimeSpan.FromSeconds(count),
                "min" => TimeSpan.FromMinutes(count),
                "h" => TimeSpan.FromHours(count),
                "d" => TimeSpan.FromDays(count),
                _ => null
            };
            return res is not null ? res.ToAny() : None;
        }
        catch (OverflowException)
        {
            return None;
        }
    }
}

public class AtParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(AtSegment);
    public override string ShownTypeName => "At";

    protected override Option<Any> TryMatchToken(SoraSegment segment)
        => segment.Data is AtSegment at && at.Target != "all" ? at.ToAny() : None;
}
