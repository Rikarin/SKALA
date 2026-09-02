using Microsoft.Win32.SafeHandles;
using System;

// `SetHandle` and `DangerousAddRef` are not the read this rule is about, and a shadowing
// overload that takes an argument is not `SafeHandle.DangerousGetHandle()`.
public sealed class Wrapper : SafeHandleZeroOrMinusOneIsInvalid {
    public Wrapper() : base(true) { }

    public void Adopt(IntPtr value) {
        SetHandle(value);
    }

    protected override bool ReleaseHandle() {
        return true;
    }
}
