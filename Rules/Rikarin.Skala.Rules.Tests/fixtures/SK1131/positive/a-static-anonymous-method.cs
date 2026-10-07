using System;

public static class Doubling {
    public static readonly Func<int, int> Twice = static delegate(int x) { return x * 2; };
}
