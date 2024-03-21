using LanguageExt;
using Sora.Entities;
using Sora.Entities.Segment;
using TairitsuSora.Core;
using TairitsuSora.Utils;

public abstract class TokenParameterMatcher : IParameterMatcher
{
    public abstract Type ParameterType { get; }
    public abstract string ShownTypeName { get; }

    protected abstract Option<Any> TryMatchToken(SoraSegment segment);

    public Either<Any, MessageBody> TryMatch(ref MessageBody msg)
    {
        var (token, remaining) = msg.SeparateFirstToken();
        var match = TryMatchToken(token);
        if (match.IsNone) return (MessageBody)token;
        msg = remaining;
        return match.Get();
    }
}
