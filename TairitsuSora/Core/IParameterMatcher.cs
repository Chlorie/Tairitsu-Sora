using Sora.Entities;
using TairitsuSora.Utils;

namespace TairitsuSora.Core;

public interface IParameterMatcher
{
    /// <summary>
    /// The undecorated type of the parameter.
    /// </summary>
    Type ParameterType { get; }

    /// <summary>
    /// The type name shown in the help message
    /// </summary>
    string ShownTypeName { get; }

    /// <summary>
    /// Try to match the beginning part of the message to this parameter.
    /// </summary>
    /// <param name="msg">The remaining part of the message.</param>
    /// <returns>
    /// The matched parameter object if the match succeeded, or the part that
    /// failed to match the parameter otherwise.
    /// </returns>
    Either<object?, MessageBody> TryMatch(ref MessageBody msg);
}
