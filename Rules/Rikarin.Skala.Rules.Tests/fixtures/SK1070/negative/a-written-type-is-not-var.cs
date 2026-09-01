public sealed class Widener {
    public long Total() {
        var counts = Counts();
        long read = counts.Item1;
        long written = counts.Item2;
        return read + written;
    }

    static (int, int) Counts() => (1, 2);
}
