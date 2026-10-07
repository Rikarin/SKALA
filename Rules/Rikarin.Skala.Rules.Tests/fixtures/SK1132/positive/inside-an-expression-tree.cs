// A tuple *literal* is CS8143 in an expression tree; reading an element is not, and the name and the
// position compile to the same field access.
using System;
using System.Linq.Expressions;

public static class Queries {
    public static Expression<Func<(int Id, string Name), int>> Key() => row => row.Item1;
}
