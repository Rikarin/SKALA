using System;

// `(Func<int, int>)(int x) => { … }` does not parse; the cast takes a unary expression.
public static class Casting {
    public static object Boxed() => (Func<int, int>)delegate(int x) { return x; };
}
