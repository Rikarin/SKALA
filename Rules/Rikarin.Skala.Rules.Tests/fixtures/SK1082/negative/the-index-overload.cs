using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // `entries[^1]` is a different rewrite with a C# 8 floor, and it is SK1060's.
    public static int Last(List<int> entries) => entries.ElementAt(^1);
}
