using System;
using System.Collections.Generic;

public static class Own {
    public static bool Any<T>(this List<T> source, Func<T, bool> predicate) => false;
}

public sealed class Registry {
    public static bool AnyReady(List<int> values) => values.Any(value => value > 0);
}
