using System;

public sealed class Documented {
    public string Describe() {
        // The pair the parser hands back.
        var pair = new Tuple<int, string>(1, "a");
        return pair.Item1 + pair.Item2;
    }
}
