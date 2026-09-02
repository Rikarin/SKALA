// A tuple literal cannot carry the argument names `Tuple.Create` accepts.
using System;

public sealed class Named {
    public string Describe() {
        var pair = Tuple.Create(item1: 1, item2: "a");
        return pair.Item1 + pair.Item2;
    }
}
