using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class InvalidOperationExceptionGuard {
    static bool ready;

    // Describes the object's own state rather than the caller's mistake, and moving it earlier is not
    // obviously right.
    public static IEnumerable<int> Values() {
        if (!ready) {
            throw new InvalidOperationException("not started");
        }

        yield return 1;
    }
}
