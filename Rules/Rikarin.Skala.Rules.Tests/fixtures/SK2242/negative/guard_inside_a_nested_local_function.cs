using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class GuardInsideANestedLocalFunction {
    public static IEnumerable<string> Lines(string text) {
        yield return Check(text);

        static string Check(string value) {
            ArgumentNullException.ThrowIfNull(value);
            return value;
        }
    }
}
