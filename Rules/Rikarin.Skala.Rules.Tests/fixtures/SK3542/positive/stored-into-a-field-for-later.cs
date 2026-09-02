using Microsoft.Win32.SafeHandles;
using System;

// The worst version: the value outlives the method, so the window in which the finalizer may
// recycle the handle is the lifetime of this object rather than of one call.
public sealed class Cached {
    readonly IntPtr raw;

    public Cached(SafeFileHandle handle) {
        raw = handle.DangerousGetHandle();
    }

    public bool IsSet => raw != IntPtr.Zero;
}
