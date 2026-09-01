using System;

// `[ThreadStatic]` on an instance field does nothing at all: an instance field already has one
// slot per object, and the runtime ignores the attribute entirely.
sealed class RequestScope {
    [ThreadStatic] int depth;

    public int Depth => depth;
}
