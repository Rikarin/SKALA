// ⚠ `==` is identity on `Tuple` and structural on `ValueTuple`. The rewrite would change what
// this method returns.
using System;

public sealed class Pairs {
    public bool IsNull() {
        var pair = new Tuple<int, string>(1, "a");
        return pair == null;
    }
}
