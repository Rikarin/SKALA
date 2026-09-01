using System;
using System.Linq.Expressions;

static class ExpressionTreeFixture {
    public static Expression<Func<int, int>> Increment() => value => value + 1;
}
