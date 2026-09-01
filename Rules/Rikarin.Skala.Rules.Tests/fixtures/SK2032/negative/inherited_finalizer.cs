// The same argument one level up: the finalizer being suppressed belongs to the base.
using System;

class Owner {
    ~Owner() { }
}

sealed class Leaf : Owner, IDisposable {
    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}
