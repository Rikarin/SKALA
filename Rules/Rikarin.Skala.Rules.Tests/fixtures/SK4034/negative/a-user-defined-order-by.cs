using System;
using System.Collections.Generic;
using System.Linq;

public static class Own {
    public static IEnumerable<T> OrderBy<T, TKey>(this List<T> source, Func<T, TKey> key) => source;
}

public sealed class Feed {
    public static IEnumerable<int> Recent(List<int> entries) =>
        entries.OrderBy(entry => entry).Where(entry => entry > 0);
}
