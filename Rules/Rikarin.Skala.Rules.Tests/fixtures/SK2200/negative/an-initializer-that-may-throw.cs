// ⚠ #423: deleting an initializer deletes its exception too. `Limits.Total / Limits.Parts` throws
// while `Parts` is zero, and the constructor never gets to overwrite anything.
public static class Limits {
    public static int Total = 10;
    public static int Parts;
}

public sealed class Share {
    int size = Limits.Total / Limits.Parts;

    public Share() {
        size = 1;
    }

    public int Size => size;
}

public static class Probe {
    public static int Run() => new Share().Size;
}
