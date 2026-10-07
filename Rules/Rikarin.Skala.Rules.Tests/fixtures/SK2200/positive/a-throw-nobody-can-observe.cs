// #431: the constructor may throw before the overwrite, but the type is sealed, declares no finalizer
// and stores `this` nowhere — so a failed construction leaves nothing that could read the value.
public sealed class Share {
    int part = 1;

    public Share(int total, int count) {
        var each = total / count;
        part = each;
    }

    public int Part => part;
}

public static class Probe {
    public static int Run() => new Share(10, 5).Part;
}
