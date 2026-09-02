using System.Collections.Generic;

public sealed class Chained {
    // The inner `if` is an arm of a chain rather than a statement in a list, so there is nowhere
    // after it to put the moved statements without inventing a block.
    public static void Record(int mode, List<int> log) {
        if (mode == 0) {
            log.Add(0);
        } else if (mode == 1) {
            log.Add(1);
            log.Add(99);
        } else {
            log.Add(2);
            log.Add(99);
        }
    }
}
