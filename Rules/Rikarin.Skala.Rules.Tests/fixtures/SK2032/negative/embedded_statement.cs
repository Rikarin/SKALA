// Deleting the statement would leave an `if` with no body, which does not compile.
using System;

sealed class Guarded : IDisposable {
    bool owned;

    public void Dispose() {
        if (owned)
            GC.SuppressFinalize(this);
    }
}
