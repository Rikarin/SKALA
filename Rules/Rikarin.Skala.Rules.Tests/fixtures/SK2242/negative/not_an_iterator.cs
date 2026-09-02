using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class NotAnIterator {
    public static IEnumerable<string> Lines(string text) {
        ArgumentNullException.ThrowIfNull(text);
        return new[] { text };
    }
}
