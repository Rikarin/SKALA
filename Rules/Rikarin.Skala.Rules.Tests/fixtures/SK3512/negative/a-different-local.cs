using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Factory {
    // The `using` owns `scratch`; what leaves is a second object nothing disposed.
    public Handle Take() {
        using var scratch = new Handle();
        var result = new Handle();
        return result;
    }
}
