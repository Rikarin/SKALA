using System;
using System.Collections.Generic;
using System.Linq.Expressions;

// The implicit indexer pattern is not expressible in an expression tree.
public sealed class Queries {
    public Expression<Func<List<string>, string>> Last() => items => items[items.Count - 1];
}
