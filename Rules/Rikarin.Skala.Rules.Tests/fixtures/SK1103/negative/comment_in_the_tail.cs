using System.Collections.Generic;

public sealed class Annotated {
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            log.Add(1);

            // The sentinel is what the retry path is measured by.
            log.Add(99);
        } else {
            log.Add(2);
            log.Add(99);
        }
    }
}
