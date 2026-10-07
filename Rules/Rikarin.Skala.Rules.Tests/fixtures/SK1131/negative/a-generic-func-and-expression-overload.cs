using System;
using System.Linq.Expressions;

public static class Rules {
    public static void Q<T>(Func<T, bool> predicate) { }

    public static void Q<T>(Expression<Func<T, bool>> predicate) { }

    public static void Use() {
        Q(delegate(int x) { return x > 0; });
    }
}
