using System;
using System.IO;
using System.Linq.Expressions;

// ⚠ #349 said this had to be declined: "an expression tree may not contain an `is` pattern"
// (CS8122), and SK1120 has no expression-tree guard. **Refuted against csc 10.0.400.** `x is T`
// with no designation is the type-test *operator*, which an expression tree has represented as
// `ExpressionType.TypeIs` since LINQ shipped; CS8122 is about the pattern forms — a constant
// pattern, a declaration pattern, a recursive pattern — and this rule emits none of them.
//
// This is a positive fixture on purpose. `FixRoundTripTests` applies the fix and re-binds the
// result, so the file is the refutation rather than a note about it: if `x is Stream` ever stops
// compiling inside an expression tree, this goes red at the bind, not at the parse.
public static class InsideAnExpressionTree {
    public static Expression<Func<object, bool>> Filter() => x => typeof(Stream).IsInstanceOfType(x);
}
