using System;

public sealed class LoadFailure : Exception {
    public LoadFailure(string message) : base(message) { }
}

public sealed class Loader {
    public static void Load(string? path) {
        if (path is null) {
            new LoadFailure("no path");
        }
    }
}
