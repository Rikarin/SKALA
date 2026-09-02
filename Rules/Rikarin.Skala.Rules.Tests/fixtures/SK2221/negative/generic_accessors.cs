// ⚠ Both accessors below name members that do NOT exist on the target, so the generic guards are the
// only thing keeping the rule quiet — which is the point. The first version of this fixture named
// `Compute`, a member that does exist, so removing the guards changed nothing and the sabotage was
// measuring a fixture that could not go red.
using System.Runtime.CompilerServices;

class Target {
    int Compute(int a) => a;
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "NotOnTheTargetAtAll")]
    public static extern int GenericMethod<T>(Target target, int a);
}

// A generic containing type, declined for the same reason.
static class GenericHolder<T> {
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "AlsoNotOnTheTarget")]
    public static extern ref int Field(Target target);
}
