using System.Reflection;

namespace TairitsuSora.Utils;

public static class ReflectionExtensions
{
    public static bool IsNullableRef(this ParameterInfo param)
        => _nullCtx.Create(param).ReadState == NullabilityState.Nullable;
    public static bool IsNullableRef(this FieldInfo field)
        => _nullCtx.Create(field).ReadState == NullabilityState.Nullable;
    public static bool IsNullableRef(this PropertyInfo prop)
        => _nullCtx.Create(prop).ReadState == NullabilityState.Nullable;

    private static NullabilityInfoContext _nullCtx = new();
}
