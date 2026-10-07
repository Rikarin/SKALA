// `ref t.Item1` and `out t.Item1` pass the same storage slot as `ref t.Low` and `out t.Low`.
public static class Bounds {
    static void Widen(ref int value) => value--;

    static void Read(out int value) => value = 10;

    public static (int Low, int High) Make() {
        (int Low, int High) bounds = (0, 0);
        Widen(ref bounds.Item1);
        Read(out bounds.Item2);
        return bounds;
    }
}
