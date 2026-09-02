// ⚠ No `unsafe` anywhere: `[UnsafeAccessor]` requires `extern`, not `unsafe`, so #310's ban on
// `unsafe` in a fixture does not touch this rule.
using System.Runtime.CompilerServices;

class Target {
    int buffer;

    int Read() => buffer;
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_buffer")]
    public static extern ref int Buffer(Target target);
}
