using System;
using System.Linq.Expressions;

// ⚠ Measured: returned from a lambda, the anonymous method decides that lambda's return type and so
// which overload its call picks. As written both calls bind to the `Func<Func<…>>` overload; as
// lambdas both are CS0121 — in an expression body and after `return` alike.
public static class Rules {
    public static void R(Func<Func<int, bool>> factory) { }

    public static void R(Func<Expression<Func<int, bool>>> factory) { }

    public static void Use() {
        R(() => delegate(int x) { return x > 0; });
        R(() => { return delegate(int x) { return x > 0; }; });
    }
}
