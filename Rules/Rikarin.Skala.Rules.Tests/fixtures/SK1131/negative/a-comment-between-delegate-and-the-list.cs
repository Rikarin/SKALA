using System;

// The deleted span runs from `delegate` to `(`; a comment inside it would be destroyed.
public static class Commented {
    public static readonly Func<int, int> Identity = delegate /* identity */ (int x) { return x; };
}
