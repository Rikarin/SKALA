using System;

// ⚠ The shape that decides whether this rule is usable at all. `if (disposing)` is the managed half,
// the finalizer passes `false`, and every throw in that branch is unreachable from `~T()`. Reporting
// here would fire on every correct implementation of the documented pattern.
class Correct : IDisposable {
    bool _disposed;

    ~Correct() {
        Dispose(false);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(Correct));
            }

            Flush();
        }

        _disposed = true;
    }

    static void Flush() { }
}

// The same, spelled with a named argument, and with the branch written the other way round.
sealed class Negated : IDisposable {
    ~Negated() {
        Dispose(disposing: false);
    }

    public void Dispose() {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing) {
        if (!disposing) {
            return;
        } else {
            throw new NotSupportedException("managed disposal is not supported here");
        }
    }
}

// An `abstract` declaring type is skipped: the body a finalized instance runs is the derived
// override, which is not this one.
abstract class Base : IDisposable {
    ~Base() {
        Dispose(false);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        throw new NotImplementedException();
    }
}
