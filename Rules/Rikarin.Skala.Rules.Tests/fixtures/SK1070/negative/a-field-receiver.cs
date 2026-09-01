public sealed class Holder {
    (int, int) bounds = (1, 2);

    public int Range() {
        var low = bounds.Item1;
        var high = bounds.Item2;
        return high - low;
    }
}
