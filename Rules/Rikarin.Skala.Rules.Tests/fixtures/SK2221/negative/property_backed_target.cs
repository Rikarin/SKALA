// A property name is accepted as a method target: a property's accessors are methods and the runtime
// finds them. A rule that matched only IMethodSymbol would report this correct accessor as broken.
using System.Runtime.CompilerServices;

class Target {
    int Value { get; set; }
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Value")]
    public static extern int Value(Target target);
}
