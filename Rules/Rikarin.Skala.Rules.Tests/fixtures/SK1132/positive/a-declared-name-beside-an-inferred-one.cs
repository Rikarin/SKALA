public sealed class Mixed {
    // `count` is inferred and is left alone; `Limit` was written, and `Item2` is reported.
    public int Use(int count) {
        var pair = (count, Limit: 10);
        return pair.Item2 - count;
    }
}
