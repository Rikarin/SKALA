using System;

public sealed class Stored {
    readonly Tuple<int, string> pair = new Tuple<int, string>(1, "a");

    public string Describe() => pair.Item1 + pair.Item2;
}
