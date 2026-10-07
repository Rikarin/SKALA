// An auto-property's accessors are the compiler's and touch only the backing field, so reading it
// twice or writing it zero times is unobservable; the fix's result is pinned by Probe (#412).
public sealed class Box {
    public string? Name { get; set; }
}

public static class Probe {
    public static int Run() {
        var box = new Box();
        box.Name = box.Name ?? "fallback";
        return box.Name.Length;
    }
}
