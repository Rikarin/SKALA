public sealed class Legacy {
    public int Range() {
        var bounds = System.Tuple.Create(1, 2);
        var low = bounds.Item1;
        var high = bounds.Item2;
        return high - low;
    }
}
