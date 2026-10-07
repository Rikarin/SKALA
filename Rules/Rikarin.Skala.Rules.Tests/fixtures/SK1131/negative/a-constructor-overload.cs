using System;
using System.Linq.Expressions;

public sealed class Rule {
    public Rule(Func<int, bool> predicate) { }

    public Rule(Expression<Func<int, bool>> predicate) { }
}

public static class Use {
    public static Rule Make() => new Rule(delegate(int x) { return x > 0; });
}
