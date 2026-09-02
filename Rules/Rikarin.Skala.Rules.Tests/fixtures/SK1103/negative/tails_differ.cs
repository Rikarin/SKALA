using System.Collections.Generic;

public sealed class Diverging {
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            log.Add(1);
            log.Add(98);
        } else {
            log.Add(2);
            log.Add(99);
        }
    }
}
