using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class YieldOnlyInLocalFunction {
    // ⚠ The `yield` belongs to the local function, so *this* method is not an iterator at all and its
    // guard runs when it is called. Reading the nested `yield` as this method's would report the one
    // shape the rule exists to recommend.
    public static IEnumerable<string> Lines(string text) {
        ArgumentNullException.ThrowIfNull(text);
        return Core();

        IEnumerable<string> Core() {
            yield return text;
        }
    }
}
