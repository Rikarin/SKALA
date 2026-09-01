// A partial type's finalizer can be in another file, so the answer is not decidable from this one
// and `scope: Semantic` promises the cache that it is.
using System;

sealed partial class Split : IDisposable {
    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}

sealed partial class Split {
}
