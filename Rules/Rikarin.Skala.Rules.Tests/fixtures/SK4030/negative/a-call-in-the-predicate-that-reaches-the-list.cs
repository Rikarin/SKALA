// ⚠ #430: the predicate never names the list, and a method it calls grows it all the same. Nothing
// short of proving the call runs no code can tell this from `name.Length > 0`, so any call to a method
// somebody wrote declines. Measured `True` before the fix and `False` after.
using System.Collections.Generic;
using System.Linq;

public sealed class Backlog {
    readonly List<int> items = new() { 1, 2 };

    bool Accept(int item) {
        if (items.Count < 3) {
            items.Add(-1);
        }

        return item > 0;
    }

    public bool AllPositive() => items.All(item => Accept(item));
}

public static class Probe {
    public static bool Run() => new Backlog().AllPositive();
}
