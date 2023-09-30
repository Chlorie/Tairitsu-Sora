using Sora.Entities;
using Sora.Entities.Segment;
using TairitsuSora.Utils;

namespace TairitsuSora.Core;

public class IntParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(int);
    public override string ShownTypeName => "int";

    protected override ResultType<object?>? TryMatchToken(SoraSegment segment)
        => segment.GetText() is { } text && int.TryParse(text, out int value)
            ? ((object?)value).AsResult() : null;
}

public class FloatParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(float);
    public override string ShownTypeName => "float";

    protected override ResultType<object?>? TryMatchToken(SoraSegment segment)
        => segment.GetText() is { } text && float.TryParse(text, out float value)
            ? ((object?)value).AsResult() : null;
}

public class StringParameterMatcher : TokenParameterMatcher
{
    public override Type ParameterType => typeof(string);
    public override string ShownTypeName => "string";

    protected override ResultType<object?>? TryMatchToken(SoraSegment segment)
        => segment.GetText() is { } text && text != "" ? ((object?)text).AsResult() : null;
}

public record struct FullString(string Text);

public class FullStringParameterMatcher : IParameterMatcher
{
    public Type ParameterType => typeof(FullString);
    public string ShownTypeName => "string+";

    public Either<object?, MessageBody> TryMatch(ref MessageBody msg)
    {
        if (msg.Count == 0 || msg[0].GetText() is not { } text)
            return new FullString("");
        msg.RemoveAt(0);
        return new FullString(text);
    }
}
