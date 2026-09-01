using System;

[Flags]
public enum Access {
    None = 0,
    Read = 1,
    Write = 2
}

public sealed class Grant {
    Access access = Access.None;

    public void AllowWrite() {
        access = access | Access.Write;
    }

    public bool CanWrite => (access & Access.Write) != 0;
}
