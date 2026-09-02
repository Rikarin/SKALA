using System;

public sealed class Documented {
    public string Describe() {
        var pair = new Tuple<int, string>(1, /* the label */ "a");
        return pair.Item1 + pair.Item2;
    }
}
