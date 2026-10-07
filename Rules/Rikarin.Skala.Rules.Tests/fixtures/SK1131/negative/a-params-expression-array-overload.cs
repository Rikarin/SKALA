using System;
using System.Linq.Expressions;

// The expression type is the element of a `params` array, not the parameter itself.
public sealed class Query<T> {
    public Query<T> Include(params Expression<Func<T, object>>[] paths) => this;

    public Query<T> Include(params Func<T, object>[] paths) => this;
}

public static class Use {
    public static Query<string> Run(Query<string> query) {
        return query.Include(delegate(string s) { return s.Length; });
    }
}
