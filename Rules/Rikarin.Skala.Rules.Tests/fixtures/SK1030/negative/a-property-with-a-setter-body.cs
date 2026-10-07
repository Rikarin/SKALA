// ⚠ `Name = Name ?? d` calls the setter every time and `Name ??= d` only when it was null — the
// INotifyPropertyChanged setter that raises on every assignment. Measured for #412's audit:
// `Probe.Run()` is 1 as written and 0 after the rewrite, so a property with a body is not storage.
public sealed class Box {
    string? name = "set";

    public int Sets;

    public string? Name {
        get => name;
        set {
            Sets++;
            name = value;
        }
    }
}

public static class Probe {
    public static int Run() {
        var box = new Box();
        box.Name = box.Name ?? "fallback";
        return box.Sets;
    }
}
