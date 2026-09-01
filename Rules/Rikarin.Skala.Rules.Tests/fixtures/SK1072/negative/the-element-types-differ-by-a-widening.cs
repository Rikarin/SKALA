public sealed class Widths {
    public double[] All(int value, double extra) => [.. new long[] { value }, extra];
}
