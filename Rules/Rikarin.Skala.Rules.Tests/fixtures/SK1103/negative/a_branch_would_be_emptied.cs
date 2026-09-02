using System.Collections.Generic;

public sealed class Consumed {
    // Hoisting the whole of one branch says the `if` had nothing to choose, which is a finding
    // about the condition rather than about the tail.
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            log.Add(99);
        } else {
            log.Add(2);
            log.Add(99);
        }
    }
}
