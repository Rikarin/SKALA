// ⚠ #423: `Make().f * 0` is always 0, but `0` does not call `Make()`. The guard accepted any field
// reference whatever its receiver; #412's audit measured the call running before the fix and not after.
public sealed class Box {
    public int f = 5;
}

public static class Probe {
    static int calls;

    static Box Make() {
        calls++;
        return new Box();
    }

    public static int Run() {
        var r = Make().f * 0;
        return r + calls;
    }
}
