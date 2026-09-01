using System.Collections.Generic;

public sealed class Registry {
    // Two reads of `Current` are two calls, and nothing here promises they hand back one object.
    public static HashSet<string> Current => [];

    public static void Merge() {
        Current.UnionWith(Current);
    }
}
