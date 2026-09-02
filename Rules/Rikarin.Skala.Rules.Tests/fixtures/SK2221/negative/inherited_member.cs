// The member is declared on the base type. The runtime walks the hierarchy and so does the rule; a
// version that stopped at the declared type would report this correct accessor as broken.
using System.Runtime.CompilerServices;

class Base {
    int inherited;

    int Helper(int a) => a + inherited;
}

class Derived : Base {
}

static class Accessors {
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "inherited")]
    public static extern ref int Inherited(Derived target);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Helper")]
    public static extern int Helper(Derived target, int a);
}
