using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class ThrowInsideTry {
    // Error translation, not an entry check: hoisting this out of the `try` would change which
    // exception the caller sees.
    public static IEnumerable<int> Parse(string text) {
        try {
            if (text.Length == 0) {
                throw new ArgumentException("empty", nameof(text));
            }
        } catch (ArgumentException) {
            yield break;
        }

        yield return text.Length;
    }
}
