using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class ThrowAfterYield {
    // A throw *after* a `yield` is already running inside the enumeration, which is where the author
    // put it on purpose.
    public static IEnumerable<string> Lines(string text, string? tail) {
        yield return text;

        if (tail is null) {
            throw new ArgumentNullException(nameof(tail));
        }

        yield return tail;
    }
}
