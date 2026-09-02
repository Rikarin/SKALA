// `UnsafeAccessorKind.Constructor` looks for `.ctor` and nothing else, so any other `Name` names
// something that will never be found. This one needs no member list, so it holds cross-assembly too.
using System.Runtime.CompilerServices;

class Target {
    Target(int seed) => Seed = seed;

    public int Seed { get; }
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Constructor, Name = "Create")]
    public static extern Target Create(int seed);
}
