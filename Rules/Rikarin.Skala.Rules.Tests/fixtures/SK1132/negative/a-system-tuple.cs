// `System.Tuple<...>.Item1` is a property of a class and the only name it has.
using System;

public static class Legacy {
    public static int First(Tuple<int, string> pair) => pair.Item1;
}
