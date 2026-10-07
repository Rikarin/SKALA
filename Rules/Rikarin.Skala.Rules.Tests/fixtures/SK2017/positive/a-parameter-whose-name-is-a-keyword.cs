// #424: the parameter is declared `@class` and named `class`; the fix writes `nameof(@class)`,
// because `nameof(class)` is CS1026.
using System;

public static class Checks {
    public static void Check(string @class) {
        if (@class.Length == 0) {
            throw new ArgumentException("bad", "clas");
        }
    }
}
