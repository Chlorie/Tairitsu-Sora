using Sora.Entities;
using Sora.Entities.Segment;
using TairitsuSora.Core;
using TairitsuSora.Utils;

public abstract class TokenParameterMatcher : IParameterMatcher
{
    public abstract Type ParameterType { get; }
    public abstract string ShownTypeName { get; }

    protected abstract ResultType<object?>? TryMatchToken(SoraSegment segment);

    public Either<object?, MessageBody> TryMatch(ref MessageBody msg)
    {
        var (token, remaining) = msg.SeparateFirstToken();
        if (TryMatchToken(token) is not { Value: var res })
            return ((MessageBody)token).AsError();
        msg = remaining;
        return res;
    }
}
