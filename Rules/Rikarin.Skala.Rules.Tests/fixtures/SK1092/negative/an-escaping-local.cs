using System;

public sealed class Pairs {
    public Tuple<int, string> Make() {
        var pair = new Tuple<int, string>(1, "a");
        return pair;
    }
}
