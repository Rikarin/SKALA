using System;
using System.Collections.Generic;

public static class Factories {
    public static Func<int, int> Returned() {
        return delegate(int x) { return x; };
    }

    public static Func<int, int> Bodied() => delegate(int x) { return -x; };

    public static IEnumerable<Func<int, int>> Yielded() {
        yield return delegate(int x) { return x + 1; };
    }
}
