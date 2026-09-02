using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class WrapperAndCore {
    // The repair this rule is about: an eager wrapper that validates and returns, and a private
    // iterator that yields.
    public static IEnumerable<string> Lines(string text) {
        ArgumentNullException.ThrowIfNull(text);
        return LinesCore(text);
    }

    static IEnumerable<string> LinesCore(string text) {
        yield return text;
    }
}
