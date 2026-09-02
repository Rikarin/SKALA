using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class GuardInsideALambda {
    public static IEnumerable<Action<string>> Validators() {
        yield return value => ArgumentNullException.ThrowIfNull(value);
    }
}
