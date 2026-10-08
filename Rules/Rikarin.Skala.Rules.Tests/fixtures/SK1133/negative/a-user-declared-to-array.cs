// fixture-option: LangVersion = 14
public sealed class Ring {
    readonly int[] slots = { 1, 2, 3 };

    public int[] ToArray() => (int[])slots.Clone();
}

public static class Reader {
    public static int[] Read(Ring ring) {
        int[] copied = ring.ToArray();
        return copied;
    }
}
