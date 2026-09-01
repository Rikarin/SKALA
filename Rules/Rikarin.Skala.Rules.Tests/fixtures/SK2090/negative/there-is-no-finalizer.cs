using System;

// A throwing `Dispose(bool)` on a type with no finalizer at all. The hop only exists from `~T()`,
// so nothing here is on any finalizer's path.
sealed class NoFinalizer : IDisposable {
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing) {
        throw new NotSupportedException("this type refuses disposal");
    }
}

// The callee belongs to another type, so it is not followed. One hop, on the declaring type only.
sealed class CallsAnotherType {
    readonly Helper _helper = new();

    ~CallsAnotherType() {
        _helper.Release();
    }
}

sealed class Helper {
    public void Release() => throw new InvalidOperationException("not followed");
}

// A finalizer that does the ordinary thing.
sealed class Ordinary {
    nint _handle;

    ~Ordinary() {
        if (_handle != 0) {
            _handle = 0;
        }
    }
}
