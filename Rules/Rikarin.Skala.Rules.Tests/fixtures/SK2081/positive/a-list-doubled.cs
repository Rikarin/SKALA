using System.Collections.Generic;

public sealed class Batch {
    public static void Extend(List<int> items) {
        items.AddRange(items);
    }
}
