using Microsoft.Win32.SafeHandles;

// The other repair, and the one that is usually right: hand the `SafeHandle` to the interop
// call and let the marshaller do the ref-counting. Nothing is dereferenced by hand.
public static class Marshalled {
    public static bool Closed(SafeFileHandle handle) {
        return handle.IsClosed || handle.IsInvalid;
    }

    public static void Release(SafeFileHandle handle) {
        handle.Dispose();
    }
}
