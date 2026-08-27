using System;
using System.Collections.Generic;
using System.Linq.Expressions;

// A statement lambda in an expression tree cannot contain an out-variable declaration.
public sealed class Registry {
    public Expression<Func<Dictionary<string, int>, string, int>> Read() =>
        (map, key) => map.ContainsKey(key) ? map[key] : 0;
}
