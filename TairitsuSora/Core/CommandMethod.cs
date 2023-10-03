using System.Reflection;
using Sora.Entities;
using Sora.Entities.Segment;
using Sora.EventArgs.SoraEvent;
using TairitsuSora.Utils;

namespace TairitsuSora.Core;

public class CommandMethod
{
    public MessageHandlerAttribute HandlerAttribute { get; }

    public static CommandMethod Create(MethodInfo method, MessageHandlerAttribute attr)
        => Create(method, attr, _defaultMatchers);

    public static CommandMethod Create(MethodInfo method, MessageHandlerAttribute attr,
        IReadOnlyDictionary<Type, IParameterMatcher> paramMatchers)
        => new(method, attr, paramMatchers);

    /// <summary>
    /// Try to match a message against this method, and construct the parameters
    /// to the method call if it succeeds.
    /// </summary>
    /// <param name="msg">The message.</param>
    /// <returns>
    /// The parameters to the method call if the match succeeded, or the error message otherwise.
    /// </returns>
    public Either<object?[], CommandMatchFailure> TryMatch(MessageBody msg)
    {
        var parameters = new object?[_methodParamCount];
        int idxOffset = _acceptsEventArgs ? 1 : 0;
        for (int i = 0; i < _paramInfos.Length; i++)
        {
            ICommandParameterInfo paramInfo = _paramInfos[i];
            var match = paramInfo.TryMatch(ref msg);
            if (match.HoldsError) return new CommandMatchFailure(i, match.Error).AsError();
            if (paramInfo.Index is { } idx) parameters[idx + idxOffset] = match.Result;
        }
        return msg.IsEmpty() ? parameters : new CommandMatchFailure(_paramInfos.Length, "指令结尾有多余的参数").AsError();
    }

    public async ValueTask Invoke(Command cmd, object?[] args, GroupMessageEventArgs eventArgs)
    {
        if (_acceptsEventArgs) args[0] = eventArgs;
        try { await _resultConverter(_method, cmd, args, eventArgs); }
        catch (Exception ex)
        {
            if (HandlerAttribute.ReplyException) await eventArgs.QuoteReply(ex.Message);
            else throw;
        }
    }

    public string SignatureDescription
        => _signatureDesc ??= string.Join(' ', _paramInfos.Select(param => param.Description));

    private delegate ValueTask GroupMessageHandlerResultConverter(
        MethodInfo method, object cmd, object?[]? args, GroupMessageEventArgs eventArgs);

    private interface ICommandParameterInfo
    {
        int? Index { get; }
        string Description { get; }
        Either<object?, string> TryMatch(ref MessageBody msg);
    }

    private class KeywordCommandParameter : ICommandParameterInfo
    {
        public int? Index => null;
        // ReSharper disable once ConvertToAutoPropertyWhenPossible
        public string Description => _keyword;

        public KeywordCommandParameter(string keyword) => _keyword = keyword;

        public Either<object?, string> TryMatch(ref MessageBody msg)
        {
            var (token, remaining) = msg.SeparateFirstToken();
            if (token.GetText() != _keyword)
                return $"未能匹配关键词 {_keyword}".AsError();
            msg = remaining;
            return null;
        }

        private string _keyword;
    }

    private class SingleCommandParameter : ICommandParameterInfo
    {
        public int? Index { get; }

        public string Description => _shownDefaultValue is null
            ? $"[{_paramName}: {ShownTypeName}]"
            : $"[{_paramName}: {ShownTypeName} = {_shownDefaultValue}]";

        public SingleCommandParameter(string paramName, int index, Type type,
            IReadOnlyDictionary<Type, IParameterMatcher> paramMatchers,
            bool nullable = false, object? defaultValue = null, string? shownDefaultValue = null)
        {
            _matcher = GetMatcherFor(type, paramMatchers);
            _paramName = paramName;
            Index = index;
            _nullable = nullable;
            _defaultValue = defaultValue;
            _shownDefaultValue = shownDefaultValue ?? defaultValue;
        }

        public Either<object?, string> TryMatch(ref MessageBody msg)
        {
            var match = _matcher.TryMatch(ref msg);
            if (match.HoldsResult) return match.Result;
            if (_nullable || _defaultValue is not null) return _defaultValue;
            return (msg.IsWhitespace()
                ? $"缺失参数 {_paramName}: {_matcher.ShownTypeName}"
                : $"无法将 {match.Error.Stringify()} 解析为参数 {_paramName}: {_matcher.ShownTypeName}")
                .AsError();
        }

        private IParameterMatcher _matcher;
        private string _paramName;
        private bool _nullable;
        private object? _defaultValue;
        private object? _shownDefaultValue;

        private string ShownTypeName
            => $"{_matcher.ShownTypeName}{(_nullable && _shownDefaultValue is null ? "?" : "")}";
    }

    private class ArrayCommandParameter : ICommandParameterInfo
    {
        public int? Index { get; }
        public string Description => $"[{_paramName}: {_matcher.ShownTypeName}...]";

        public ArrayCommandParameter(string paramName, int index, Type type,
            IReadOnlyDictionary<Type, IParameterMatcher> paramMatchers)
        {
            _matcher = GetMatcherFor(type, paramMatchers);
            _paramName = paramName;
            Index = index;
        }

        public Either<object?, string> TryMatch(ref MessageBody msg)
        {
            List<object?> list = new();
            while (true)
            {
                var match = _matcher.TryMatch(ref msg);
                if (match.HoldsError) break;
                list.Add(match.Result);
            }
            Array array = Array.CreateInstance(_matcher.ParameterType, list.Count);
            for (int i = 0; i < list.Count; i++) array.SetValue(list[i], i);
            return array;
        }

        private IParameterMatcher _matcher;
        private string _paramName;
    }

    private static Dictionary<Type, GroupMessageHandlerResultConverter> _typedConverters;
    private static Dictionary<Type, IParameterMatcher> _defaultMatchers;
    private MethodInfo _method;
    private bool _acceptsEventArgs;
    private int _methodParamCount;
    private GroupMessageHandlerResultConverter _resultConverter;
    private IReadOnlyDictionary<Type, IParameterMatcher> _paramMatchers;
    private ICommandParameterInfo[] _paramInfos = Array.Empty<ICommandParameterInfo>();
    private string? _signatureDesc;

    static CommandMethod()
    {
        _typedConverters = MakeConverters();
        _defaultMatchers = MakeDefaultMatchers();
    }

    private static Dictionary<Type, GroupMessageHandlerResultConverter> MakeConverters()
    {
        Dictionary<Type, GroupMessageHandlerResultConverter> res = new()
        {
            [typeof(void)] = static (method, cmd, args, _) =>
            {
                method.Invoke(cmd, args);
                return ValueTask.CompletedTask;
            },
            [typeof(Task)] = static (method, cmd, args, _)
                => ((Task)method.Invoke(cmd, args)!).AsValueTask(),
            [typeof(ValueTask)] = static (method, cmd, args, _)
                => (ValueTask)method.Invoke(cmd, args)!
        };
        AddConverters(MessageBody (string text) => SoraSegment.Text(text));
        AddConverters(MessageBody (SoraSegment seg) => seg);
        AddConverters((MessageBody body) => body);
        return res;

        void AddConverters<T>(Func<T, MessageBody> msgConverter)
        {
            res.Add(typeof(T), async (method, cmd, args, eventArgs)
                => await eventArgs.QuoteReply(msgConverter((T)method.Invoke(cmd, args)!)));
            res.Add(typeof(Task<T>), async (method, cmd, args, eventArgs)
                => await eventArgs.QuoteReply(msgConverter(await (Task<T>)method.Invoke(cmd, args)!)));
            res.Add(typeof(ValueTask<T>), async (method, cmd, args, eventArgs)
                => await eventArgs.QuoteReply(msgConverter(await (ValueTask<T>)method.Invoke(cmd, args)!)));
        }
    }

    private static Dictionary<Type, IParameterMatcher> MakeDefaultMatchers()
    {
        Dictionary<Type, IParameterMatcher> res = new();
        void AddMatcher(IParameterMatcher matcher) => res[matcher.ParameterType] = matcher;
        AddMatcher(new IntParameterMatcher());
        AddMatcher(new FloatParameterMatcher());
        AddMatcher(new StringParameterMatcher());
        AddMatcher(new FullStringParameterMatcher());
        AddMatcher(new TimeSpanParameterMatcher());
        AddMatcher(new AtParameterMatcher());
        return res;
    }

    private static IParameterMatcher GetMatcherFor(Type type,
        IReadOnlyDictionary<Type, IParameterMatcher> paramMatchers)
    {
        if (!paramMatchers.TryGetValue(type, out var proc))
            throw new ArgumentException($"Type {type.Name} cannot be processed by the given parameter processors");
        return proc;
    }

    private CommandMethod(MethodInfo method, MessageHandlerAttribute attr,
        IReadOnlyDictionary<Type, IParameterMatcher> paramMatchers)
    {
        _method = method;
        HandlerAttribute = attr;
        if (!_typedConverters.TryGetValue(method.ReturnType, out var conv))
            throw new ArgumentException($"Invalid return type {method.ReturnType} for a message handler");
        _resultConverter = conv;
        _paramMatchers = paramMatchers;
        InitializeParamInfos();
    }

    private void InitializeParamInfos()
    {
        ReadOnlySpan<ParameterInfo> parameters = _method.GetParameters();
        _methodParamCount = parameters.Length;
        if (parameters.Length > 0 && parameters[0].ParameterType == typeof(GroupMessageEventArgs))
        {
            _acceptsEventArgs = true;
            parameters = parameters[1..];
        }
        string[] signatureParts = HandlerAttribute.Signature.SplitByWhitespaces();
        var paramsCovered = new bool[parameters.Length];
        _paramInfos = new ICommandParameterInfo[signatureParts.Length];
        for (int i = 0; i < _paramInfos.Length; i++)
        {
            var info = _paramInfos[i] = GetCmdParamInfo(signatureParts[i], parameters);
            if (info.Index is not { } idx) continue;
            if (paramsCovered[idx])
                throw new ArgumentException($"Parameter {parameters[idx].Name} is referenced multiple times");
            paramsCovered[idx] = true;
        }
        for (int i = 0; i < paramsCovered.Length; i++)
            if (!paramsCovered[i])
                throw new ArgumentException($"Parameter {parameters[i].Name} isn't referenced in the command signature");
    }

    private ICommandParameterInfo GetCmdParamInfo(string part, ReadOnlySpan<ParameterInfo> methodParams)
    {
        if (!part.StartsWith('$')) return new KeywordCommandParameter(part);
        string paramName = part[1..];
        int index = -1;
        for (int i = 0; i < methodParams.Length; i++)
            if (methodParams[i].Name == paramName)
            {
                index = i;
                break;
            }
        if (index == -1)
            throw new ArgumentException($"Cannot find a parameter named {paramName} in message handler method {_method.Name}");
        ParameterInfo param = methodParams[index];
        if (CheckSingleCmdParamInfo(param, index) is { } single) return single;
        if (CheckArrayCmdParamInfo(param, index) is { } array) return array;
        throw new NullReferenceException("Unknown error when matching command parameters");
    }

    private ICommandParameterInfo? CheckSingleCmdParamInfo(ParameterInfo param, int index)
    {
        bool nullable;
        Type paramType = param.ParameterType, elemType = paramType;
        if (paramType.IsArray) return null;
        if (Nullable.GetUnderlyingType(paramType) is { } elem)
        {
            nullable = true;
            elemType = elem;
        }
        else
            nullable = param.IsNullableRef();
        return new SingleCommandParameter(param.Name!, index, elemType, _paramMatchers, nullable,
            param.HasDefaultValue ? param.DefaultValue : null,
            param.GetCustomAttribute<ShowDefaultValueAsAttribute>()?.Value);
    }

    private ICommandParameterInfo? CheckArrayCmdParamInfo(ParameterInfo param, int index)
    {
        Type paramType = param.ParameterType;
        if (!paramType.IsArray) return null;
        if (paramType.GetArrayRank() != 1)
            throw new InvalidOperationException("Array parameter of a message handler cannot be multidimensional");
        Type elemType = paramType.GetElementType()!;
        return new ArrayCommandParameter(param.Name!, index, elemType, _paramMatchers);
    }
}

public record struct CommandMatchFailure(int MatchedParameterCount, string Message);
