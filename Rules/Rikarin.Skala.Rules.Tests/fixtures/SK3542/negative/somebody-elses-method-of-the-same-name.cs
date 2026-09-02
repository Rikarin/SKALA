using System;

// A `DangerousGetHandle` of someone's own invention on a type that is not a `SafeHandle`. The
// name is matched syntactically first and then proved through the semantic model, and this is
// the case the second half exists for.
public sealed class Pseudo {
    public IntPtr DangerousGetHandle() {
        return new IntPtr(1);
    }
}

public static class Caller {
    public static IntPtr Use(Pseudo value) {
        return value.DangerousGetHandle();
    }
}
