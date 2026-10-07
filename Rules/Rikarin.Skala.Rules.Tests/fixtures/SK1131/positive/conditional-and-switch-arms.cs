using System;

public static class Choosing {
    public static Func<int, int> Pick(int mode, bool flag) {
        Func<int, int> chosen = flag ? delegate(int x) { return x; } : delegate(int x) { return -x; };
        return mode switch {
            0 => chosen,
            _ => delegate(int x) { return x * mode; }
        };
    }
}
