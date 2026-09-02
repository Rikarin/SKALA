// Only `ItemN` reads count. Every other member is a place the two types could differ.
using System;

public sealed class Printed {
    public string Describe() {
        var pair = new Tuple<int, string>(1, "a");
        return pair.ToString();
    }
}
