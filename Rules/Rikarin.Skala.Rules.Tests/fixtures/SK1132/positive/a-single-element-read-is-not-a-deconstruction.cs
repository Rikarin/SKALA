// SK1070's own negative shape: one read on its own is not a deconstruction missing half of itself, so
// SK1070 says nothing — and the read still ignores the name the type gives it.
public sealed class Measurement {
    public int Low() {
        var bounds = Bounds();
        var low = bounds.Item1;
        return low;
    }

    static (int Low, int High) Bounds() => (0, 10);
}
