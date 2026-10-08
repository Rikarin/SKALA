// fixture-option: LangVersion = 14
using System.Collections.Generic;

public static class MyExtensions {
    public static T[] ToArray<T>(this IReadOnlyCollection<T> source) {
        var result = new T[source.Count];
        var i = 0;
        foreach (var item in source) {
            result[result.Length - ++i] = item;
        }

        return result;
    }
}

public static class Reversed {
    public static int[] Copy(IReadOnlyCollection<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
