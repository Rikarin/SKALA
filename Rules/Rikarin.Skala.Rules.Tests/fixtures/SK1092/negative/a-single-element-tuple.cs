// `(x)` is a parenthesized expression, not a one-element tuple literal.
using System;

public sealed class Singles {
    public int Value() {
        var one = new Tuple<int>(1);
        return one.Item1;
    }
}
