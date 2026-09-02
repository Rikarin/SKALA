using System.Collections.Generic;

public sealed class Scoped {
    // ⚠ The name guard. `step` is declared inside each branch, so a `log.Add(step)` moved below the
    // `if` would name something that no longer binds.
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            var step = 1;
            log.Add(step);
        } else {
            var step = 2;
            log.Add(step);
        }
    }
}
