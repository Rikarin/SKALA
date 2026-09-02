using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class ThrowIfNullHelper {
    public static IEnumerable<int> Each(int[] values) {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var value in values) {
            yield return value;
        }
    }
}
