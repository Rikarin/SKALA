using System;

// ⚠ #412's audit: the `goto` jumps over the declaration to a label in its scope, which a `using`
// declaration makes CS8648.
public sealed class Resource : IDisposable {
    public int Uses;

    public void Use() => Uses++;

    public void Dispose() { }
}

public static class Probe {
    static int Jump(bool skip) {
        var result = 0;
        if (skip) {
            goto done;
        }

        var resource = new Resource();
        resource.Use();
        result = resource.Uses;
        done:
        return result;
    }

    public static int Run() => Jump(false) + Jump(true);
}
