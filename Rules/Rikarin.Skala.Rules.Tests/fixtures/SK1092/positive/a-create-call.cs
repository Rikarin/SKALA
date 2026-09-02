using System;

public sealed class Triples {
    public int Sum() {
        var triple = Tuple.Create(1, 2, 3);
        return triple.Item1 + triple.Item2 + triple.Item3;
    }
}
