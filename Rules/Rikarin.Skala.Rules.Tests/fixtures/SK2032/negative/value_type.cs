// A struct cannot have a finalizer either, so the call is redundant here too — but it also boxes,
// and that is a different finding with a different repair.
using System;

struct Slot : IDisposable {
    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}
