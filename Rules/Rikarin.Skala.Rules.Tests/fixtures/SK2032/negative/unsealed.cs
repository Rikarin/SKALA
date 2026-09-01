// ⚠ Not redundant: a derived type may declare a finalizer, and this call is what suppresses it.
using System;

class Base : IDisposable {
    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}
