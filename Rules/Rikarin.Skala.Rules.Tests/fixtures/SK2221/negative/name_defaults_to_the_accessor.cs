// With no `Name`, the accessor's own name is the name looked for. A rule that treated the absent
// `Name` as an empty string would report both of these.
using System.Runtime.CompilerServices;

class Target {
    int buffer;

    int Compute(int a) => a + buffer;
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Field)]
    public static extern ref int buffer(Target target);

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    public static extern int Compute(Target target, int a);
}
