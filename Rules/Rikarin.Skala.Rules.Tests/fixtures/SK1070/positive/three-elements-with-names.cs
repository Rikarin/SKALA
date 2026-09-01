public sealed class Measurement {
    public int Range() {
        var bounds = Bounds();
        var low = bounds.Item1;
        var high = bounds.Item2;
        var step = bounds.Item3;
        return (high - low) / step;
    }

    static (int Low, int High, int Step) Bounds() => (0, 10, 2);
}
