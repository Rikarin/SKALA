// A generic accessor is skipped: which generic shapes the runtime accepts is version-dependent and
// the analyzer cannot know which runtime this assembly will load on. A property-backed name is
// accepted as a method target, because a property's accessors are methods and the runtime finds them.
using System.Runtime.CompilerServices;

class Target {
    int Value { get; set; }

    int Compute(int a) => a + Value;
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Compute")]
    public static extern int Compute<T>(Target target, int a);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Value")]
    public static extern int Value(Target target);
}
