using System;

public sealed class Pairs {
    public string Describe() {
        Tuple<int, string> pair = new Tuple<int, string>(1, "a");
        return pair.Item1 + pair.Item2;
    }
}
