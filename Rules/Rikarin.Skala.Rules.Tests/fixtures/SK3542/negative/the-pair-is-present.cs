using Microsoft.Win32.SafeHandles;
using System;

// The documented shape. Whether the pair brackets this call correctly is a flow question the
// rule does not ask — it withdraws on the presence of the ref-counting, and says so.
public static class Held {
    public static IntPtr Read(SafeFileHandle handle) {
        var taken = false;
        try {
            handle.DangerousAddRef(ref taken);
            return handle.DangerousGetHandle();
        } finally {
            if (taken) {
                handle.DangerousRelease();
            }
        }
    }
}
