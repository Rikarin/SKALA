using System;
using System.Linq.Expressions;

// Measured: the anonymous method binds to the `Func` overload; the lambda is CS0121, ambiguous with
// the `Expression` one — a lambda is applicable to an expression tree, an anonymous method is not.
public static class Rules {
    public static void Q(Func<int, bool> predicate) { }

    public static void Q(Expression<Func<int, bool>> predicate) { }

    public static void Use() {
        Q(delegate(int x) { return x > 0; });
    }
}
