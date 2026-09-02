using Microsoft.Win32.SafeHandles;
using System;

public static class Reader {
    public static IntPtr Peek(SafeFileHandle handle) {
        return handle.DangerousGetHandle();
    }
}
