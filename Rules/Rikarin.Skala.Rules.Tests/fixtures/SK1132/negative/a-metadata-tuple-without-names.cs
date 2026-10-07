// `ValueTuple.Create` returns `ValueTuple<int, int>` with no `TupleElementNamesAttribute` behind it.
using System;

public static class Factory {
    public static int First() => ValueTuple.Create(1, 2).Item1;
}
