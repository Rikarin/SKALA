using System.Collections.Generic;

public sealed class Registry {
    public static int Ready(List<int> values) => values.Find(value => value > 0);

    public static bool AnyReady(List<int> values) => values.Exists(value => value > 0);

    public static bool AllReady(List<int> values) => values.TrueForAll(value => value > 0);

    public static bool Knows(List<string> names, string wanted) => names.Contains(wanted);
}
