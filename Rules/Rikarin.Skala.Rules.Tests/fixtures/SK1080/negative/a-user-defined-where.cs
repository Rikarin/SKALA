using System.Collections.Generic;

public static class Own {
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, System.Func<T, bool> predicate) => source;

    public static IEnumerable<TResult> Cast<TResult>(this IEnumerable<object> source) => [];
}

public sealed class Registry {
    // Neither call is the framework's; what they do is somebody else's business.
    public static IEnumerable<string> Names(IEnumerable<object> values) =>
        values.Where(value => value is string).Cast<string>();
}
