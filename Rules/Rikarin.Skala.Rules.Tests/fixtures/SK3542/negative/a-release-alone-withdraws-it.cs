using Microsoft.Win32.SafeHandles;
using System;

// Either half of the pair withdraws the finding. A type that calls `DangerousRelease` knows
// about the reference count, and whether it acquired one correctly is not this rule's claim.
public sealed class Halfway {
    public IntPtr Read(SafeFileHandle handle) {
        try {
            return handle.DangerousGetHandle();
        } finally {
            handle.DangerousRelease();
        }
    }
}
