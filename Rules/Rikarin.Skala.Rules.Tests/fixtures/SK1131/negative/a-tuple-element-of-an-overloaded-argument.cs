using System;
using System.Linq.Expressions;

// ⚠ Measured: a tuple element is target-typed by the tuple parameter, so the tuple is transparent —
// as written this binds to the `Func` tuple; as a lambda it is CS0121.
public static class Rules {
    public static void Q((int Order, Func<int, bool> Predicate) rule) { }

    public static void Q((int Order, Expression<Func<int, bool>> Predicate) rule) { }

    public static void Use() {
        Q((1, delegate(int x) { return x > 0; }));
    }
}
