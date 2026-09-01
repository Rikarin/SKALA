using System;

// The shape the rule exists for: the destructor is one line and the throw is one hop away.
sealed class Handle : IDisposable {
    nint _handle;

    ~Handle() {
        Dispose(false);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing) {
        if (_handle == 0) {
            throw new InvalidOperationException("the handle was already released");
        }

        _handle = 0;
    }
}

// `virtual` on a concrete type: `new Virtual()` finalized runs this body.
class Virtual : IDisposable {
    ~Virtual() {
        Dispose(false);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            Flush();
        }

        throw new NotSupportedException("unmanaged release is not implemented");
    }

    static void Flush() { }
}

// The `else` of `if (!disposing)` is the finalizer's own branch, so the throw is reached.
sealed class NegatedBranch : IDisposable {
    ~NegatedBranch() {
        Dispose(false);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing) {
        if (!disposing) {
            throw new InvalidOperationException("no unmanaged state to release");
        }
    }
}
