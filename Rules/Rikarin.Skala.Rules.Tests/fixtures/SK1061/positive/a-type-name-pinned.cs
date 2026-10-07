// An ordinary named type's metadata name is its source name, nested or not. Pinned by Probe (#425).
public sealed class Widget {
    public sealed class Part {
    }
}

public enum Shade {
    Light,
    Dark
}

public static class Probe {
    public static string Run() => typeof(Widget).Name + "/" + typeof(Widget.Part).Name + "/" + Shade.Dark.ToString();
}
