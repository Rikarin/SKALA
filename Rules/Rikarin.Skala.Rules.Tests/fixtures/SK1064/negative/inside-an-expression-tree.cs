using System;
using System.Linq.Expressions;

// The rewrite would change what the tree contains.
public sealed class Queries {
    public Expression<Func<int, int>> High() => hash => (int)((uint)hash >> 16);
}
