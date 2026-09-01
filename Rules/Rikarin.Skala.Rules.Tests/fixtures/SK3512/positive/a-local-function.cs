using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Factory {
    public Handle Take() {
        return Build();

        static Handle Build() {
            using var handle = new Handle();
            return handle;
        }
    }
}
