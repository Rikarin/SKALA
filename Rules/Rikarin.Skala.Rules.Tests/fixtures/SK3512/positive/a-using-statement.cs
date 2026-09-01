using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Factory {
    public Handle Take() {
        using (var handle = new Handle()) {
            return handle;
        }
    }
}
