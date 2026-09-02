using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class NoGuardAtAll {
    public static IEnumerable<int> Values(int count) {
        for (var i = 0; i < count; i++) {
            yield return i;
        }
    }
}
