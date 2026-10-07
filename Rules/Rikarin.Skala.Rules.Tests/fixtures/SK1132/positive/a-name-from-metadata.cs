// `Math.DivRem` is compiled with `TupleElementNamesAttribute`, so its names survive the assembly boundary.
using System;

public static class Division {
    public static int Remainder(int a, int b) => Math.DivRem(a, b).Item2;
}
