using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Factory {
    // A parameter is not declared by any `using`; the caller's object goes back to the caller.
    public Handle Pass(Handle handle) {
        return handle;
    }
}
