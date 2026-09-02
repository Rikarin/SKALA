using System.Collections.Generic;

public sealed class Unbraced {
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            log.Add(1);
            log.Add(99);
        } else
            log.Add(99);
    }
}
