using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class ThrowBeforeYield {
    public static IEnumerable<string> Lines(string text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        yield return text;
    }
}
