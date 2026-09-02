using Microsoft.Win32.SafeHandles;
using System;

// The question is asked of the whole type rather than the enclosing method, because the
// documented pattern splits the `AddRef` and the `Release` across a wrapper's `try`/`finally`.
public sealed class Scoped {
    readonly SafeFileHandle handle;
    bool taken;

    public Scoped(SafeFileHandle target) {
        handle = target;
    }

    public void Acquire() {
        handle.DangerousAddRef(ref taken);
    }

    public IntPtr Raw() {
        return handle.DangerousGetHandle();
    }

    public void Release() {
        if (taken) {
            handle.DangerousRelease();
        }
    }
}
