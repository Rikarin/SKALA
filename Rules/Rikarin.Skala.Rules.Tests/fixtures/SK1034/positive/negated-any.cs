using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    public static bool Empty(List<int> items) {
        return !items.Any();
    }
}
