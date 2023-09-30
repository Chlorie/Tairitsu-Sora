namespace TairitsuSora.Utils;

public struct Either<TRes, TError>
{
    public bool HoldsResult => _value is ResultType<TRes>;
    public bool HoldsError => _value is ErrorType<TError>;
    public TRes Result => ((ResultType<TRes>)_value).Value;
    public TError Error => ((ErrorType<TError>)_value).Value;

    public static Either<TRes, TError> FromResult(TRes result) => new(new ResultType<TRes>(result));
    public static Either<TRes, TError> FromError(TError error) => new(new ErrorType<TError>(error));
    public static implicit operator Either<TRes, TError>(TRes result) => new(new ResultType<TRes>(result));
    public static implicit operator Either<TRes, TError>(ResultType<TRes> result) => new(result);
    public static implicit operator Either<TRes, TError>(ErrorType<TError> error) => new(error);

    public TRes ResultOr(TRes defaultValue) => HoldsResult ? Result : defaultValue;

    private object _value;

    private Either(ResultType<TRes> resType) => _value = resType;
    private Either(ErrorType<TError> errorType) => _value = errorType;
}

public record struct ResultType<T>(T Value);
public record struct ErrorType<T>(T Value);

public static class EitherExtensions
{
    public static ResultType<T> AsResult<T>(this T value) => new(value);
    public static ErrorType<T> AsError<T>(this T value) => new(value);
}
