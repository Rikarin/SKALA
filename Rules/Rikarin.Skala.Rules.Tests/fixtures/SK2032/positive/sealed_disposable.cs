using System;

sealed class Reader : IDisposable {
    public void Dispose() {
        Close();
        GC.SuppressFinalize(this);
    }

    void Close() { }
}
