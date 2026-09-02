using Microsoft.Win32.SafeHandles;
using System;

public sealed class Optional {
    public IntPtr Read(SafeFileHandle? handle) {
        return handle?.DangerousGetHandle() ?? IntPtr.Zero;
    }
}
