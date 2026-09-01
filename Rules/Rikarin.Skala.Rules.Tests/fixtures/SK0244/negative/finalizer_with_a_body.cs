using System;

sealed class Buffer {
    IntPtr handle;

    ~Buffer() => handle = IntPtr.Zero;

    public IntPtr Handle => handle;
}
