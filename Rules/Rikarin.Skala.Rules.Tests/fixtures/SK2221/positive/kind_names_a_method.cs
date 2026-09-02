// The name exists on the target, but it is a method and the accessor asks for a field, so the
// runtime looks in the field table and finds nothing.
using System.Runtime.CompilerServices;

class Target {
    int Compute(int a) => a + 1;
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Compute")]
    public static extern ref int Compute(Target target);
}
