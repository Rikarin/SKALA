public sealed class Pair {
    public int Item1 => 1;

    public int Item2 => 2;
}

public sealed class Reader {
    public int Range() {
        var pair = new Pair();
        var low = pair.Item1;
        var high = pair.Item2;
        return high - low;
    }
}
