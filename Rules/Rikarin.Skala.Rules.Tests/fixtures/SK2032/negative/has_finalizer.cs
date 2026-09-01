using System;

sealed class Native : IDisposable {
    public void Dispose() {
        GC.SuppressFinalize(this);
    }

    ~Native() { }
}
