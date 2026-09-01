public sealed class Reporter {
    public int Total((int, int) counts) {
        var read = counts.Item1;
        var written = counts.Item2;
        return read + written;
    }
}
