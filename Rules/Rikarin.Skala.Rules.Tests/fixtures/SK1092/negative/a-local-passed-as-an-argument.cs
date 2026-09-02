using System;

public sealed class Pairs {
    public string Describe() {
        var pair = new Tuple<int, string>(1, "a");
        return Render(pair);
    }

    static string Render(Tuple<int, string> value) => value.Item2;
}
