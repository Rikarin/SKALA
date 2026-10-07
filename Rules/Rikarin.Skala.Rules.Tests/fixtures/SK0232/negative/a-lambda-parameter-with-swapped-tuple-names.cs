using System;

// The written type names the elements the other way round, so `t.a` is the delegate's `b`. Deleting
// the type would make `t.a` the delegate's `a`: tuple names are part of what the type says here.
public static class Pairs {
    public static int First((int a, int b) pair) {
        Func<(int a, int b), int> pick = ((int b, int a) t) => t.a;
        return pick(pair);
    }
}

public static class Probe {
    public static int Run() => Pairs.First((1, 2));
}
