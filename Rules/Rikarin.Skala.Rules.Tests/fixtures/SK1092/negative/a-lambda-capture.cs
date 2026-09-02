using System;

public sealed class Captured {
    public Func<Tuple<int, string>> Make() {
        var pair = new Tuple<int, string>(1, "a");
        return () => pair;
    }
}
