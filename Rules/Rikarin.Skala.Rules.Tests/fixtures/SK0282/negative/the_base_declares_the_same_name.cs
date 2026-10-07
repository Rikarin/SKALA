// ⚠ #424: a positional property is synthesized only where no member of that name is declared or
// inherited. Deleting `Derived`'s `X` binds the parameter to `Base.X`, which `Base(X * 10)` set to
// 10; #412's audit measured 1 becoming 10.
public record Base(int X);

public record Derived(int X) : Base(X * 10) {
    public int X { get; init; } = X;
}

public static class Probe {
    public static int Run() => new Derived(1).X;
}
