// ⚠ #423: `b.f * 0` throws when `b` is null and `0` does not. Dropping an evaluation includes
// dropping its exception, which #412's audit measured (`NullReferenceException` → `0`).
public sealed class Box {
    public int f = 5;
}

public static class Probe {
    static int Scaled(Box b) => b.f * 0;

    public static int Run() => Scaled(null!);
}
