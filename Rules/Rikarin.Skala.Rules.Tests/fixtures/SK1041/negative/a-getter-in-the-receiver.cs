// ⚠ #423: the long form evaluates the receiver `h.Current` twice — once as the place to store and
// once to read — and `h.Current.X += 5` evaluates it once. Measured for #412's audit: two reads
// before the fix, one after. A getter with a body is not storage.
public sealed class Box {
    public int X;
}

public sealed class Holder {
    readonly Box[] boxes = { new Box(), new Box() };

    public int Reads;

    public Box Current => boxes[Reads++ % 2];
}

public static class Probe {
    public static int Run() {
        var h = new Holder();
        h.Current.X = h.Current.X + 5;
        return h.Reads;
    }
}
