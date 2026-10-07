// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
using System;
using System.Collections.Generic;
using System.Linq;

namespace Skala.Corpus.Arrangement;

// #392: the shapes SK0209 used to keep whole on the belief that the oracle leaves them alone — a
// lambda, a query, a conditional, an assignment, and anything in an interpolation hole — and the
// non-binary operands of a non-obvious operation. The oracle removes each of them wherever the parse
// allows and keeps exactly the ones the parse needs.
//
// ⚠ No multi-line `switch` expression here, although #392 is mostly about them: the oracle chops every
// one, and a parenthesised multi-line `switch` is indented a level too deep by Skala's formatter
// (#393), so a kept one would pin that defect in this file's format-only twin. The `switch` rows are
// in RedundantParenthesesIssue392Tests, arranged and never formatted; the interpolation holes below
// are the `switch` rows the oracle leaves on one line.
public class ParenthesesShapes {
    // Removed: a lambda in every position but the right of `??`.
    public Func<int> Lambda() => (() => 1);

    public Func<int, Func<int, int>> CurriedLambda() => x => (y => x + y);

    public void LambdaArgument(List<int> l) => l.ForEach((x => Console.WriteLine(x)));

    // Kept: `f ?? () => 1` does not parse.
    public Func<int> LambdaCoalesce(Func<int>? f) => f ?? (() => 1);

    // Removed: a query, including as the source of another query.
    public IEnumerable<int> QueryArgument(int[] a) => a.Concat((from x in a select x));

    public IEnumerable<int> QueryInFrom(int[] a) => from x in (from y in a select y) select x;

    public IEnumerable<int> QueryWhere(int[] a, int b) => from x in a where (x > b) select x;

    // Kept: the parse needs them before `.`.
    public int QueryCount(int[] a) => (from x in a select x).Count();

    // Removed: a conditional, nested in either branch.
    public int ConditionalArgument(bool b) => Math.Abs((b ? 1 : 2));

    public int ConditionalElse(bool a, bool b) => a ? 1 : (b ? 2 : 3);

    public int ConditionalThen(bool a, bool b) => a ? (b ? 1 : 2) : 3;

    // Kept: without them the condition would be `!b ? 1 : 2`.
    public int ConditionalCondition(bool a, bool b) => (a ? b : !b) ? 1 : 2;

    // Removed: an assignment, chained or returned.
    public int Assignment(int a) {
        int x, y;
        x = (y = a);
        return (x = x + y);
    }

    // Removed: an interpolation hole, with a format or an alignment clause after it.
    public string Hole(int a, int b) => $"{(a + b)}";

    public string HoleFormat(int a, int b) => $"{(a + b):D2}";

    public string HoleSwitch(int v) => $"{(v switch { 1 => 10, _ => 0 })}";

    public string HoleSwitchAligned(int v) => $"{(v switch { 1 => 10, _ => 0 }),5}";

    // Kept: the `:` would become a format clause.
    public string HoleConditional(bool b) => $"{(b ? 1 : 2)}";

    // Removed: `resharper_parentheses_non_obvious_operations` keeps a *binary* operand of `&` or `<<`,
    // and `is` and `as` are not counted as one.
    public int BitwiseUnary(int a, int b) => a & (-b);

    public int ShiftUnary(int a, int b) => a << (-b);

    public int BitwiseMember(int[] a, int b) => b & (a.Length);

    public bool BitwiseIs(bool b, object o) => b & (o is string);

    // Kept: a binary operand.
    public bool BitwiseEquality(bool b, int x) => b & (x == 1);

    // Removed, and the comment stays.
    public int Commented(int a, int b) => ( /* why */ a + b);
}
