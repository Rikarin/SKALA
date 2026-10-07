using System;
using System.Linq.Expressions;

// The walk stops at the nearest call. `Wrap` infers `T` from the natural type, which is the same
// delegate type for both spellings, so `R` never sees a lambda — measured to bind `R(Func<…>)` both
// ways.
public static class Rules {
    public static void R(Func<int, bool> predicate) { }

    public static void R(Expression<Func<int, bool>> predicate) { }

    static T Wrap<T>(T value) => value;

    public static void Use() {
        R(Wrap(delegate(int x) { return x > 0; }));
    }
}
