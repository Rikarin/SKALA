using System;
using System.Linq.Expressions;

// The anonymous method is not the argument — a conditional is — but it still takes part in the
// outer call's overload resolution, so every enclosing argument is asked.
public static class Rules {
    public static void Q(Func<int, bool> predicate) { }

    public static void Q(Expression<Func<int, bool>> predicate) { }

    public static void Use(bool strict) {
        Q(strict ? delegate(int x) { return x > 0; } : delegate(int x) { return x >= 0; });
    }
}
