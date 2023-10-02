using JetBrains.Annotations;

namespace TairitsuSora.Core;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[MeansImplicitUse]
public class RegisterCommandAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class MessageHandlerAttribute : Attribute
{
    public string Signature { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>
    /// If any exception is caught in the handling process, the exception message
    /// is quote replied to the original message.
    /// </summary>
    public bool ReplyException { get; set; }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class ShowDefaultValueAsAttribute : Attribute
{
    public string Value { get; }
    public ShowDefaultValueAsAttribute(string value) => Value = value;
}
