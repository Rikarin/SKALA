using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

// A collection-initializer element is an argument to `Add`, and `Add` is overloaded here.
public sealed class Filters : IEnumerable<object> {
    readonly List<object> items = [];

    public void Add(Func<int, bool> predicate) => items.Add(predicate);

    public void Add(Expression<Func<int, bool>> predicate) => items.Add(predicate);

    public IEnumerator<object> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class Use {
    public static Filters Make() => new Filters { delegate(int x) { return x > 0; } };
}
