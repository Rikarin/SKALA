using System.Collections.Generic;

public static class Own {
    public static List<T> ToList<T>(this IEnumerable<T> source) => [];
}

public sealed class Registry {
    public static List<int> Ids(IEnumerable<int> source) => source.ToList().ToList();
}
