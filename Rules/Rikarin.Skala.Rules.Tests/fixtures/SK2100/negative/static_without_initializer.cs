using System;
using System.Text;

// The shape the attribute exists for: a static field, no initializer, filled lazily per thread.
static class Scratch {
    [ThreadStatic] static StringBuilder? buffer;

    public static StringBuilder Buffer => buffer ??= new StringBuilder();
}
