using System;
using System.Linq.Expressions;

// ⚠ The one `typeof` operand whose rewrite is a pattern rather than a type test. The type is
// re-spelled from source text, so this would emit `x is (int, int)` — and the parser reads text
// starting with `(` down the *pattern* path, giving an `IsPatternExpressionSyntax`. Outside an
// expression tree that compiles and asks the identical question; inside one it is **CS8122**,
// compiled and confirmed rather than reasoned about.
//
// ⚠ The guard is on the syntax, not on `INamedTypeSymbol.IsTupleType`: `typeof(ValueTuple<int, int>)`
// is the same symbol and emits `x is ValueTuple<int, int>`, which the parser accepts as a type and
// which SK1120 therefore still reports here.
public static class TupleTargetInsideAnExpressionTree {
    public static Expression<Func<object, bool>> Filter() => x => typeof((int, int)).IsInstanceOfType(x);
}
