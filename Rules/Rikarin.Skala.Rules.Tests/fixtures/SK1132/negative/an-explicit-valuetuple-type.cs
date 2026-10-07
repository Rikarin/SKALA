// `ValueTuple<int, string>` written out cannot carry names, so `Item1` is all there is.
using System;

public static class Explicit {
    public static int First(ValueTuple<int, string> pair) => pair.Item1;
}
