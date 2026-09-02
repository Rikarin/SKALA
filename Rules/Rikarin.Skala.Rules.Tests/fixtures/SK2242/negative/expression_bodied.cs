using System;
using System.Collections.Generic;
using System.Linq;

namespace Fixtures.SK2242;

public static class ExpressionBodied {
    // An expression-bodied method cannot contain `yield`, so it is never an iterator.
    public static IEnumerable<string> Lines(string text) =>
        text.Split('\n').AsEnumerable();
}
