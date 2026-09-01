public sealed class Counter {
    int reads;

    (int, int) Bounds {
        get {
            reads++;
            return (reads, reads * 2);
        }
    }

    public int Range() {
        var low = Bounds.Item1;
        var high = Bounds.Item2;
        return high - low;
    }
}
