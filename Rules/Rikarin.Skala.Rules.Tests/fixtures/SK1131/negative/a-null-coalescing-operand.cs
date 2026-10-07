using System;

// A lambda is not a primary expression: `fallback ?? (int x) => { … }` does not parse.
public static class Defaults {
    public static Func<int, int> Or(Func<int, int>? fallback) {
        return fallback ?? delegate(int x) { return x; };
    }
}
