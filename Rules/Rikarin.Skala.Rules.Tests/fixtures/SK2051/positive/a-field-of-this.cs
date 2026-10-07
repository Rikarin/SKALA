// A field of `this` cannot throw when it is read and runs nothing, so discarding its evaluation is
// unobservable; the fix's result is pinned by Probe (#423).
public sealed class Mask {
    int bits = 6;

    public int Bits => bits;

    public int Cleared() => this.bits & 0;
}

public static class Probe {
    public static int Run() {
        var mask = new Mask();
        return mask.Cleared() + mask.Bits;
    }
}
