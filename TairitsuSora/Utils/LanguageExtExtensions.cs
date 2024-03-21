using LanguageExt;

namespace TairitsuSora.Utils;

public static class LanguageExtExtensions
{
    public static TL GetLeft<TL, TR>(this Either<TL, TR> either) => (TL)either;
    public static TR GetRight<TL, TR>(this Either<TL, TR> either) => (TR)either;
    public static T Get<T>(this Option<T> option) => (T)option;
}
