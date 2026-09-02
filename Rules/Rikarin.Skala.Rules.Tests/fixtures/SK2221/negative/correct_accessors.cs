// Every kind, spelled correctly. The rule must be silent on all of them.
using System.Runtime.CompilerServices;

class Target {
    int buffer;
    static int shared;

    Target(int seed) => buffer = seed;

    int Compute(int a) => a + buffer;

    static int StaticCompute(int a) => a + shared;
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "buffer")]
    public static extern ref int Buffer(Target target);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "shared")]
    public static extern ref int Shared(Target target);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Compute")]
    public static extern int Compute(Target target, int a);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "StaticCompute")]
    public static extern int StaticCompute(Target target, int a);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    public static extern Target Create(int seed);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor, Name = ".ctor")]
    public static extern Target CreateExplicit(int seed);
}
