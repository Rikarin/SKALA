using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    public static void Use(List<int> items) {
        if (items.Any()) {
            items.Clear();
        }
    }
}
