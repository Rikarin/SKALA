using System;

// The arrow is inserted directly after `)`, so the comment after it is not inside any edit.
public static class Commented {
    public static readonly Func<int, int> Identity = delegate(int x) /* identity */ { return x; };
}
