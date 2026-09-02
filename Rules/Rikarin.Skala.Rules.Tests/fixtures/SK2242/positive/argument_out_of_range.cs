using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class ArgumentOutOfRange {
    public static IEnumerable<int> Take(int count) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        for (var i = 0; i < count; i++) {
            yield return i;
        }
    }
}
