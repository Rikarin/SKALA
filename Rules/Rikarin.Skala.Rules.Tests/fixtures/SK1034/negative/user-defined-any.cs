using System.Collections.Generic;

public static class Extensions {
    public static bool Any(this List<int> items) => items.Count > 0;
}

public sealed class Holder {
    public static bool Has(List<int> items) => items.Any();
}
