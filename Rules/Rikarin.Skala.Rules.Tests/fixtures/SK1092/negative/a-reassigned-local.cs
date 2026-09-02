using System;

public sealed class Reassigned {
    public string Describe(bool second) {
        var pair = new Tuple<int, string>(1, "a");
        if (second) {
            pair = new Tuple<int, string>(2, "b");
        }

        return pair.Item1 + pair.Item2;
    }
}
