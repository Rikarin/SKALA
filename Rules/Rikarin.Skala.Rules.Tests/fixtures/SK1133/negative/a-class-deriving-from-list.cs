// fixture-option: LangVersion = 14
// ⚠ `mine.ToArray()` is `List<int>.ToArray`, but `[..mine]` lowers to `Enumerable.ToArray`: a null
// receiver would throw ArgumentNullException where it threw NullReferenceException.
using System.Collections.Generic;

public sealed class Mine : List<int>;

public static class Probe {
    static Mine Missing() => null!;

    public static string Run() {
        try {
            int[] copied = Missing().ToArray();
            return copied.Length.ToString();
        } catch (System.Exception exception) {
            return exception.GetType().Name;
        }
    }
}
