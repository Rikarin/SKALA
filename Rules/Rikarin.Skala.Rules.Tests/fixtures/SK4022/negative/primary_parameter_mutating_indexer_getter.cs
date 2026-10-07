// ⚠ An indexer getter is a method call on the capture like any other (#412).
public struct Cursor {
    public int Reads;

    public int this[int offset] {
        get {
            Reads++;
            return Reads + offset;
        }
    }
}

struct Reader(Cursor cursor) {
    public int Peek() => cursor[0];

    public int Reads => cursor.Reads;
}

public static class Probe {
    public static int Run() {
        var reader = new Reader(new Cursor());
        reader.Peek();
        return reader.Reads;
    }
}
