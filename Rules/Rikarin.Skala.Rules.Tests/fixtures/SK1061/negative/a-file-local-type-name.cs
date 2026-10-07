// ⚠ #425: a `file` type's metadata name is mangled so that two files may each declare one, and
// `Type.Name` reads the metadata name. Measured for #412's audit: `<p>F…__Widget` before the fix and
// `Widget` after it.
file sealed class Widget {
}

public static class Probe {
    public static bool Run() => typeof(Widget).Name == "Widget";
}
